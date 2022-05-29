using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Controllers.Model;
using NetworkSoundBox.Controllers.Model.Request;
using NetworkSoundBox.Controllers.Model.Response;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Filter;
using NetworkSoundBox.Middleware.Hubs;
using NetworkSoundBox.Middleware.Logger;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.Message;
using NetworkSoundBox.Services.Model;
using NetworkSoundBox.Services.TextToSpeech;
using Newtonsoft.Json;
using Nsb.Type;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Hosting;
using NetworkSoundBox.Services.Audios;
using static NetworkSoundBox.Controllers.Model.Response.FailureDevice;

namespace NetworkSoundBox.Controllers
{
    [Route("api/device_ctrl")]
    [ApiController]
    [ServiceFilter(typeof(ResourceAuthAttribute))]
    public class DeviceController : ControllerBase
    {
        private readonly MySqlDbContext _dbContext;
        private readonly IDeviceContext _deviceContext;
        private readonly IXunfeiTtsService _xunfeiTtsService;
        private readonly INotificationContext _notificationContext;
        private readonly ILogger<DeviceController> _logger;
        private readonly string _audioRootPath;
        private readonly IAudioProcessorHelper _helper;

        private string UserReferenceId { get => GetUserReferenceId(); }

        public DeviceController(
            MySqlDbContext dbContext,
            ILogger<DeviceController> logger,
            INotificationContext notificationContext,
            IXunfeiTtsService xunfeiTtsService,
            IDeviceContext deviceContext,
            IConfiguration configuration,
            IAudioProcessorHelper helper)
        {
            _dbContext = dbContext;
            _logger = logger;
            _deviceContext = deviceContext;
            _xunfeiTtsService = xunfeiTtsService;
            _notificationContext = notificationContext;
            _helper = helper;

            _audioRootPath = configuration["AudioRootPath"];
        }

        /// <summary>
        /// 获取播放列表
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("play_list")]
        public IActionResult GetPlayList([FromQuery] string sn)
        {
            if (!CheckPermission(sn, PermissionType.View)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePool[sn];
            int result = device.GetPlayList();
            return result != -1 ? Ok(result) : BadRequest("设备未响应");
        }

        /// <summary>
        /// 获取设备音频
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("get_audios")]
        public IActionResult GetDeviceAudios([FromQuery] string sn)
        {
            if (string.IsNullOrEmpty(sn))
                return BadRequest("请求为空");

            try
            {
                var deviceAudioEntities = (from deviceAudio in _dbContext.DeviceAudios
                                           join userDevice in _dbContext.UserDevices
                                           on deviceAudio.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                           join device in _dbContext.Devices
                                           on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                                           join audio in _dbContext.Audios
                                           on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                                           where userDevice.UserRefrenceId == UserReferenceId
                                           where device.Sn == sn
                                           select new
                                           {
                                               AudioReferenceId = deviceAudio.AudioReferenceId,
                                               Index = deviceAudio.Index,
                                               IsSynced = deviceAudio.IsSynced,
                                               AudioName = audio.AudioName,
                                               Size = audio.Size
                                           }).ToList();
                var unknownAudioEntities = (from deviceAudio in _dbContext.DeviceAudios
                                            join userDevice in _dbContext.UserDevices
                                                on deviceAudio.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                            join device in _dbContext.Devices
                                                on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                                            where userDevice.UserRefrenceId == UserReferenceId
                                            where deviceAudio.AudioReferenceId == null
                                            where device.Sn == sn
                                            select new
                                            {
                                                AudioReferenceId = string.Empty,
                                                Index = deviceAudio.Index,
                                                IsSynced = deviceAudio.IsSynced,
                                                AudioName = "未知音频",
                                                Size = 0
                                            }).ToList();
                deviceAudioEntities.AddRange(unknownAudioEntities);
                return Ok(JsonConvert.SerializeObject(deviceAudioEntities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While GetDeviceAudios is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 添加设备音频
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("add_audio")]
        public IActionResult AddDeviceAudios([FromBody] AddDeviceAudiosRequest request)
        {
            if (string.IsNullOrEmpty(request.Sn))
                return BadRequest("设备参数有误");
            if (string.IsNullOrEmpty(request.AudioReferenceId))
                return BadRequest("音频参数有误");

            try
            {
                if (IsInGroup(request.Sn)) return BadRequest("处于设备组的设备不可编辑音频");
                var deviceAudioEntitie = (from deviceAudio in _dbContext.DeviceAudios
                                           join device in _dbContext.Devices
                                           on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                                           join userDevice in _dbContext.UserDevices
                                           on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                           where userDevice.UserRefrenceId == UserReferenceId
                                           where device.Sn == request.Sn
                                           where deviceAudio.AudioReferenceId == request.AudioReferenceId
                                           select deviceAudio).FirstOrDefault();
                if (deviceAudioEntitie != null)
                    return Ok();

                var device_audio = (from device in _dbContext.Devices
                                    join userDevice in _dbContext.UserDevices
                                    on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                    where userDevice.UserRefrenceId == UserReferenceId
                                    where device.Sn == request.Sn
                                    from audio in _dbContext.Audios
                                    join cloud in _dbContext.Clouds
                                    on audio.CloudReferenceId equals cloud.CloudReferenceId
                                    where cloud.UserReferenceId == UserReferenceId
                                    where audio.AudioReferenceId == request.AudioReferenceId
                                    select new
                                    {
                                        Device = device,
                                        Audio = audio
                                    }).FirstOrDefault();

                if (device_audio.Device == null || device_audio.Audio == null)
                    return BadRequest("参数有误");

                deviceAudioEntitie = new DeviceAudio()
                {
                    DeviceReferenceId = device_audio.Device.DeviceReferenceId,
                    AudioReferenceId = device_audio.Audio.AudioReferenceId,
                    IsSynced = "N"
                };
                _dbContext.DeviceAudios.Add(deviceAudioEntitie);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddDeviceAudios is invoked");
                return BadRequest(ex.Message);
            }
        }

        //[Authorize(Policy = "Permission")]
        //[HttpPost("delete_audio")]
        //public IActionResult DeleteDeviceAudio([FromBody] DeleteDeviceAudioRequest request)
        //{
        //    if (string.IsNullOrEmpty(request.Sn)) 
        //        return BadRequest("设备参数有误");
        //    if (string.IsNullOrEmpty(request.AudioReferenceId))
        //        return BadRequest("音频参数有误");

        //    try
        //    {
        //        if (IsInGroup(request.Sn))
        //            return BadRequest("处于设备组的设备不可编辑音频");

        //        var deviceAudioEntity = (from deviceAudio in _dbContext.DeviceAudios
        //                                 join device in _dbContext.Devices
        //                                 on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
        //                                 join userDevice in _dbContext.UserDevices
        //                                 on deviceAudio.DeviceReferenceId equals userDevice.DeviceRefrenceId
        //                                 where userDevice.UserRefrenceId == UserReferenceId
        //                                 where device.Sn == request.Sn
        //                                 where deviceAudio.AudioReferenceId == request.AudioReferenceId
        //                                 select deviceAudio).FirstOrDefault();
        //        if (deviceAudioEntity == null)
        //            return Ok();
        //        _dbContext.DeviceAudios.Remove(deviceAudioEntity);
        //        _dbContext.SaveChanges();
        //        return Ok();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "While DeleteDeviceAudio is invoked");
        //        return BadRequest(ex.Message);
        //    }
        //}

        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("play_list_group")]
        public IActionResult GetPlayListGroup([FromBody] GetPlayListGroupRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceGroupReferenceId))
            {
                return BadRequest("非法请求");
            }

            var userReferenceId = GetUserReferenceId();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            var response = new GetPlayListGroupResponse();

            try
            {
                var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferenceId);

                deviceEntities.ForEach(deviceEntity =>
                {
                    if (_deviceContext.DevicePool.TryGetValue(deviceEntity.Sn, out var device))
                    {
                        if (!CheckPermission(deviceEntity.Sn, PermissionType.Control))
                        {
                            response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.PermissionDenied));
                        }
                        else
                        {
                            var tempAudioCount = device.GetPlayList();
                            if (tempAudioCount == -1)
                            {
                                response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.DeviceNoResponed));
                            }
                            else
                            {
                                response.SuccessDevices.Add(new SuccessDevice(deviceEntity, tempAudioCount));
                                if (tempAudioCount < response.MinAudioCount)
                                {
                                    response.MinAudioCount = tempAudioCount;
                                }
                            }
                        }
                    }
                });
                if (response.MinAudioCount == int.MaxValue)
                {
                    response.MinAudioCount = 0;
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While GetPlayListGroup is invoked");
                return BadRequest(ex.Message);
            }
            return Ok(response);
        }

        /// <summary>
        /// 删除指定音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("delete_audio")]
        public IActionResult DeleteAudio([FromQuery] string sn, int index)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");

            if (IsInGroup(sn))
                return BadRequest("处于设备组的设备不可编辑音频");

            DeviceHandler deviceHandler = _deviceContext.DevicePool[sn];

            var result = deviceHandler.DeleteAudio(index);
            if (result)
            {
                var deviceAudioEntities = (from deviceAudio in _dbContext.DeviceAudios
                                         join device in _dbContext.Devices
                                         on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                                         where device.Sn == sn
                                         orderby deviceAudio.Index
                                         select deviceAudio).ToList();

                var deleteDeviceAudioEntity = (from deviceAudio in deviceAudioEntities
                                               where deviceAudio.Index == index
                                               select deviceAudio).FirstOrDefault();

                if (deleteDeviceAudioEntity != null)
                {

                    _dbContext.DeviceAudios.Remove(deleteDeviceAudioEntity);
                    _dbContext.SaveChanges();
                    var updatingDeviceAudioEntities = deviceAudioEntities.Skip(index).ToList();
                    updatingDeviceAudioEntities.ForEach(item =>
                    {
                        item.Index--;
                    });
                    _dbContext.DeviceAudios.UpdateRange(updatingDeviceAudioEntities);
                    _dbContext.SaveChanges();
                }
            }

            return result ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("play_index")]
        public IActionResult PlayIndex([FromQuery] string sn, int index)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");

            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.PlayIndex(index) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 播放或暂停
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 播放; 2: 暂停</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("play_pause")]
        public IActionResult PlayOrPause([FromQuery] string sn, int action)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");

            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendPlayOrPause(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组控制播放暂停
        /// </summary>
        /// <param name="request">DeviceGroupReferenceId:String Action:Int(1:播放，2:暂停)</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("play_pause_group")]
        public IActionResult PlayOrPauseGroup([FromBody] PlayOrPauseGroupRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceGroupReferenceId) || 
                (request.Action != 1 && request.Action != 2))
            {
                return BadRequest("非法请求");
            }

            var userReferenceId = GetUserReferenceId();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            var response = new PlayOrPauseGroupResponse();
            try
            {

                var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferenceId);

                deviceEntities.ForEach(deviceEntity =>
                {
                    if (_deviceContext.DevicePool.TryGetValue(deviceEntity.Sn, out var device))
                    {
                        if (!CheckPermission(deviceEntity.Sn, PermissionType.Control))
                        {
                            response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.PermissionDenied));
                        }
                        else
                        {
                            if (!device.SendPlayOrPause(request.Action))
                            {
                                response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.DeviceNoResponed));
                            }
                            else
                            {
                                response.SuccessDevices.Add(new SuccessDevice(deviceEntity, null));
                            }
                        }
                    }
                });
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While PlayOrPauseGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 上一首或下一首
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 下一首; 2: 上一首</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("next_previous")]
        public IActionResult NextOrPrevious([FromQuery] string sn, int action)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            _logger.LogInformation("NextOrPrevious");
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendNextOrPrevious(action) ? Ok() : BadRequest("设备未响应");
        }


        /// <summary>
        /// 设备组下一首或上一首
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("next_previous_group")]
        public IActionResult NextOrPreviousGroup([FromBody] NextOrPreviousGroupRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceGroupReferenceId) ||
                (request.Action != 1 && request.Action != 2))
            {
                return BadRequest("非法请求");
            }

            var userReferencecId = GetUserReferenceId();
            if (string.IsNullOrEmpty(userReferencecId))
            {
                return Forbid();
            }

            var response = new NextOrPreviousGroupResponse();

            try
            {
                var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferencecId);
                deviceEntities.ForEach(deviceEntity =>
                {
                    if (_deviceContext.DevicePool.TryGetValue(deviceEntity.Sn, out var device))
                    {
                        if (!CheckPermission(deviceEntity.Sn, PermissionType.Control))
                        {
                            response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.PermissionDenied));
                        }
                        else
                        {

                            if (device.SendNextOrPrevious(request.Action))
                            {
                                response.SuccessDevices.Add(new SuccessDevice(deviceEntity, null));
                            }
                            else
                            {
                                response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.DeviceNoResponed));
                            }
                        }
                    }
                });
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While NextOrPreviousGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 设备音量
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="volume">音量(0~30)</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("volume")]
        public IActionResult Volume([FromQuery] string sn, int volume)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendVolume(volume) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组音量
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("volume_group")]
        public IActionResult VolumeGroup([FromBody] VolumeGroupRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceGroupReferenceId) ||
                (request.Action < 0 || request.Action > 30))
            {
                return BadRequest("非法请求");
            }

            var userReferenceId = GetUserReferenceId();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferenceId);

            var response = new VolumeGroupResponse();

            try
            {
                deviceEntities.ForEach(deviceEntity =>
                    {
                        if (_deviceContext.DevicePool.TryGetValue(deviceEntity.Sn, out var device))
                        {
                            if (!CheckPermission(deviceEntity.Sn, PermissionType.Control))
                            {
                                response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.PermissionDenied));
                            }
                            else
                            {
                                if (!device.SendVolume(request.Action))
                                {
                                    response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.DeviceNoResponed));
                                }
                                else
                                {
                                    response.SuccessDevices.Add(new SuccessDevice(deviceEntity, null));
                                }
                            }
                        }
                    });
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While VolumeGroup is invoked");
                return BadRequest(ex.Message);
            }
        }
        
        /// <summary>
        /// 设备重启
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("reboot")]
        public IActionResult Reboot([FromQuery] string sn)
        {
            if (!CheckPermission(sn, PermissionType.Admin)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendReboot() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组重启
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("reboot_group")]
        public IActionResult RebootGroup([FromBody] RebootGroupRequest request) 
        {
            if (string.IsNullOrEmpty(request.DeviceGroupReferenceId))
            {
                return BadRequest("非法请求");
            }

            var userReferenceId = GetUserReferenceId();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            var response = new RebootGroupResponse();

            try
            {
                var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferenceId);
                deviceEntities.ForEach(deviceEntity =>
                {
                    if (_deviceContext.DevicePool.TryGetValue(deviceEntity.Sn, out var device))
                    {
                        if (!CheckPermission(device.Sn, PermissionType.Control))
                        {
                            response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.PermissionDenied));
                        }
                        else
                        {
                            if (!device.SendReboot())
                            {
                                response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.DeviceNoResponed));
                            }
                            else
                            {
                                response.SuccessDevices.Add(new SuccessDevice(deviceEntity, null));
                            }
                        }
                    }
                });
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While RebootGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 设备重置
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("restore")]
        public IActionResult Restore([FromQuery] string sn)
        {
            if (!CheckPermission(sn, PermissionType.Admin)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendRestore() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组重置
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("restore_group")]
        public IActionResult RestoreGroup([FromBody] RestoreGroupRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceGroupReferenceId))
            {
                return BadRequest("非法请求");
            }

            var userReferenceId = GetUserReferenceId();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                Forbid();
            }

            var response = new RestoreGroupResponse();

            try
            {
                var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferenceId);
                deviceEntities.ForEach(deviceEntity =>
                {
                    if (_deviceContext.DevicePool.TryGetValue(deviceEntity.Sn, out var device))
                    {
                        if (!CheckPermission(deviceEntity.Sn, PermissionType.Admin))
                        {
                            response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.PermissionDenied));
                        }
                        else
                        {
                            if (!device.SendRestore())
                            {
                                response.FailureDevices.Add(new FailureDevice(deviceEntity, FailureType.DeviceNoResponed));
                            }
                            else
                            {
                                response.SuccessDevices.Add(new SuccessDevice(deviceEntity, null));
                            }
                        }
                    }
                });
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While RestoreGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 批量下发定时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dtos"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("cron_tasks")]
        public IActionResult SetCronTasks([FromQuery] string sn, [FromBody] IList<CronTaskDto> dtos)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            var device = _deviceContext.DevicePool[sn];

            if (!device.SendCronTaskCount(dtos.Count)) return BadRequest("设置失败");

            foreach (var dto in dtos)
            {
                // ReSharper disable once CollectionNeverQueried.Local
                List<byte> data = new()
                {
                    (byte) dto.Index,
                    (byte) dto.StartTime.Hour,
                    (byte) dto.StartTime.Minute,
                    (byte) dto.EndTime.Hour,
                    (byte) dto.EndTime.Minute,
                    (byte) dto.Volume,
                    (byte) (dto.Relay ? 0x01 : 0x00)
                };
                int? audioIndex = (from deviceAudio in _dbContext.DeviceAudios
                                  join deviceEntity in _dbContext.Devices
                                  on deviceAudio.DeviceReferenceId equals deviceEntity.DeviceReferenceId
                                  where deviceAudio.AudioReferenceId == dto.AudioReferenceId
                                  && deviceEntity.Sn == sn
                                  && deviceAudio.IsSynced == "Y"
                                  select deviceAudio.Index).FirstOrDefault();
                if (audioIndex != null)
                {
                    data.Add((byte)audioIndex);
                    dto.Weekdays.ForEach(d => { data.Add((byte)(d + 1)); });
                    if (!device.SendCronTask(data)) return BadRequest("设置失败");
                }
            }

            return Ok();
        }

        ///// <summary>
        ///// 下发定时任务
        ///// </summary>
        ///// <param name="sn"></param>
        ///// <param name="dto"></param>
        ///// <returns></returns>
        //[Authorize]
        //[Authorize(Policy = "Permission")]
        //[HttpPost("cron_task")]
        //public IActionResult SetAlarms([FromQuery] string sn, [FromBody] CronTaskDto dto)
        //{
        //    if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
        //    var device = _deviceContext.DevicePool[sn];

        //    List<byte> data = new()
        //    {
        //        (byte) dto.Index,
        //        (byte) dto.StartTime.Hour,
        //        (byte) dto.StartTime.Minute,
        //        (byte) dto.EndTime.Hour,
        //        (byte) dto.EndTime.Minute,
        //        (byte) dto.Volume,
        //        (byte) (dto.Relay ? 0x01 : 0x00)
        //    };
        //    dto.Weekdays.ForEach(d => { data.Add((byte) (d + 1)); });
        //    data.Add((byte) dto.Audio);

        //    return device.SendCronTask(data) ? Ok() : BadRequest("设备未响应");
        //}

        /// <summary>
        /// 下发延时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("delay_task")]
        public IActionResult SetAlarmsAfter([FromQuery] string sn, [FromBody] DelayTaskDto dto)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            var device = _deviceContext.DevicePool[sn];

            List<byte> data = new()
            {
                (byte) ((dto.DelaySeconds & 0xFF00) >> 8),
                (byte) (dto.DelaySeconds & 0x00FF),
                (byte) dto.Volume,
                (byte) (dto.Relay ? 0x01 : 0x00),
                (byte) dto.Audio
            };

            return device.SendDelayTask(data) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("file/sn{sn}")]
        public async Task<IActionResult> TransFile(string sn, IFormFile formFile)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            // 读取文件到内存
            var content = new byte[formFile.Length];
            await formFile.OpenReadStream().ReadAsync(content);
            //设置文件名为sn_timestamp.mp3格式，保存在本地硬盘
            var fileName = $"{sn}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.mp3";
            const string path = "./Uploaded";
            var fullPath = path + "/" + fileName;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            FileStream fileStream = new(fullPath, FileMode.CreateNew);
            BinaryWriter binaryWriter = new(fileStream);
            binaryWriter.Write(content);
            binaryWriter.Close();
            fileStream.Close();

            var device = _deviceContext.DevicePool[sn];
            // WiFi 设备传输
            if (device.Type == Nsb.Type.DeviceType.WiFi_Test)
            {
                var fileUploadHandle = new Services.Message.File(content.ToList());
                device.FileQueue.Add(fileUploadHandle);
                fileUploadHandle.Semaphore.WaitOne();
                return fileUploadHandle.FileStatus == FileStatus.Success ? Ok() : BadRequest("文件传输失败");
            }
            // 4G 设备传输
            else
            {
                AudioTrxModel dto = new()
                {
                    FileName = fileName,
                };
                var fileToken = Guid.NewGuid().ToString("N")[..8];
                _deviceContext.AudioDict.Add(fileToken, dto);
                if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");
                return await dto.Wait() ? Ok() : BadRequest("下载超时");
            }
        }

        /// <summary>
        /// 上传TTS音频到设备
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="text"></param>
        /// <param name="vcn"></param>
        /// <param name="speed"></param>
        /// <param name="volume"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("upload_tts")]
        public async Task<IActionResult> UploadTts([FromQuery] string sn, string text, VCN vcn = VCN.XIAOYAN,
            int speed = 50, int volume = 50)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            // 读取文件到内存
            var speech = await _xunfeiTtsService.GetSpeech(text, vcn.ToString().ToLower(), speed, volume);
            var speechContent = new byte[speech.Count];
            speech.CopyTo(speechContent, 0);

            // 写文件到本地硬盘
            var fileName = $"{sn}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.mp3";
            const string filePath = "./Tts";
            var fullPath = $"{filePath}/{fileName}";
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
            FileStream fileStream = new(fullPath, FileMode.CreateNew);
            BinaryWriter binaryWriter = new(fileStream);
            binaryWriter.Write(speechContent);
            binaryWriter.Close();
            fileStream.Close();

            // 判断设备类型并下发数据
            var device = _deviceContext.DevicePool[sn];
            switch (device.Type)
            {
                case Nsb.Type.DeviceType.WiFi_Test:
                {
                    var fileUploadHandler = new Services.Message.File(speech);
                    device.FileQueue.Add(fileUploadHandler);
                    fileUploadHandler.Semaphore.WaitOne();
                    return fileUploadHandler.FileStatus == FileStatus.Success ? Ok() : BadRequest("文件传输失败");
                }
                case Nsb.Type.DeviceType.Cellular_Test:
                {
                    var audioTransferDto = new AudioTrxModel()
                    {
                        FileName = fileName,
                    };
                    var fileToken = Guid.NewGuid().ToString("N")[..8];
                    _deviceContext.AudioDict.Add(fileToken, audioTransferDto);
                    if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");
                    return await audioTransferDto.Wait() ? Ok() : BadRequest("下载超时");
                }
                default:
                    return BadRequest("不支持该设备类型");
            }
        }

        /// <summary>
        /// 设备调用，下载文件
        /// </summary>
        [HttpGet("download_file")]
        public IActionResult Download_File([FromQuery] string fileToken)
        {
            if (_deviceContext.FileList.TryGetValue(fileToken, out var filePair))
            {
                FileContentResult file = new(filePair.Value.FileContents, "audio/mp3");
                filePair.Key.Release();
                return file;
            }
            else
            {
                return BadRequest("非法参数");
            }
        }

        ///// <summary>
        ///// 流式传输文件
        ///// </summary>
        ///// <param name="fileToken"></param>
        ///// <returns></returns>
        //[HttpGet("download_file_stream")]
        //public async Task<IActionResult> DownloadFileStream([FromQuery] string fileToken)
        //{
        //    if (!_deviceContext.AudioDict.TryGetValue(fileToken, out AudioTrxModel audioHandler))
        //        return new EmptyResult();
        //    try
        //    {
        //        FileInfo fileInfo = new($"{audioHandler.FilePath}/{audioHandler.FileName}");
        //        byte[] contentBuffer = new byte[fileInfo.Length];
        //        fileInfo.OpenRead().Read(contentBuffer, 0, contentBuffer.Length);

        //        var contentDisposition = $"attachment;filename={HttpUtility.UrlEncode(fileInfo.Name)}";
        //        Response.Headers.Add("Content-Disposition", new[] {contentDisposition});
        //        Response.ContentType = "audio/mp3";
        //        Response.ContentLength = contentBuffer.Length;
        //        await using (Response.Body)
        //        {
        //            var hasSent = 0;

        //            while (hasSent < contentBuffer.Length)
        //            {
        //                if (HttpContext.RequestAborted.IsCancellationRequested) break;
        //                var sendLength = contentBuffer.Length - hasSent < 1024 ? contentBuffer.Length - hasSent : 1024;
        //                ReadOnlyMemory<byte> readOnlyMemory = new(contentBuffer, hasSent, sendLength);
        //                await Response.Body.WriteAsync(readOnlyMemory);
        //                hasSent += sendLength;
        //                await _notificationContext.SendDownloadProgress(100.0f * hasSent / contentBuffer.Length, audioHandler.Sn);
        //                await Response.Body.FlushAsync();
        //            }

        //            audioHandler.TransferCplt(true);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        _deviceContext.AudioDict.Remove(fileToken);
        //        audioHandler.TransferCplt(false);
        //    }

        //    return new EmptyResult();
        //}

        [HttpGet("download_file_stream")]
        public async Task<IActionResult> DownloadFileStreamNew([FromQuery] string fileToken)
        {
            if (!_deviceContext.AudioDict.TryGetValue(fileToken, out var audioHandler))
                return new EmptyResult();
            var result = false;
            try
            {
                var fileContent = await System.IO.File.ReadAllBytesAsync($"{_audioRootPath}{audioHandler.AudioPath}");

                var contentDisposition = $"attachment;filename={DateTime.UtcNow.Ticks.ToString()}.mp3";
                Response.Headers.Add("Content-Disposition", new[] { contentDisposition });
                Response.ContentType = "audio/mp3";
                Response.ContentLength = fileContent.Length;

                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                result = UpdateDeviceAudio(audioHandler.DeviceAudioKey);
                if (!result) return new EmptyResult();

                await HttpSendFile(fileContent);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceControlApi, ex, "While DownloadFileStreamNew is invoked");
            }
            finally
            {
                _deviceContext.AudioDict.Remove(fileToken);
                audioHandler.TransferCplt(result);
            }
            return new EmptyResult();
        }

        [HttpPost("sync_device_audio")]
        public IActionResult SyncDeviceAudio([FromQuery] string sn)
        {
            if (string.IsNullOrEmpty(sn)) return BadRequest("请求为空");

            var deviceAudioEntities =
                (from deviceAudio in _dbContext.DeviceAudios
                    join userDevice in _dbContext.UserDevices 
                        on deviceAudio.DeviceReferenceId equals userDevice.DeviceRefrenceId
                    join audio in _dbContext.Audios
                        on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                    join device in _dbContext.Devices
                        on deviceAudio.DeviceReferenceId equals  device.DeviceReferenceId
                    where userDevice.UserRefrenceId == UserReferenceId &&
                          device.Sn == sn && 
                          deviceAudio.IsSynced == "N"
                    select new
                    {
                        DeviceAudio = deviceAudio,
                        Audio = audio,
                        Device = device
                    }).ToList();
            if (!deviceAudioEntities.Any()) return BadRequest("没有未同步音频");
            
            deviceAudioEntities.ForEach(audio =>
            {
                _helper.AddAudioSyncEvent(new AudioSyncEvent()
                {
                    AudioPath = audio.Audio.AudioPath,
                    DeviceAudioKey = audio.DeviceAudio.DeviceAudioKey,
                    DeviceReferenceId = audio.Device.DeviceReferenceId,
                    FileName = audio.Audio.AudioName,
                    OperationType = OperationType.Add,
                    Sn = audio.Device.Sn
                });
            });
            return Ok();
        }

        private bool CheckPermission(string sn, PermissionType limit)
        {
            if (string.IsNullOrEmpty(UserReferenceId)) return false;

            var userEntity = _dbContext.Users.First(u => u.UserRefrenceId == UserReferenceId);
            var deviceEntity = _dbContext.Devices.First(d => d.Sn == sn);
            var permission = _dbContext.UserDevices
                .Where(ud => ud.DeviceRefrenceId == deviceEntity.DeviceReferenceId && ud.UserRefrenceId == userEntity.UserRefrenceId)
                .Select(ud => ud.Permission)
                .FirstOrDefault();
            return permission <= (int)limit;
        }

        private List<Device> GetGroupDevices(string deviceGroupReferenceId, string userReferenceId)
        {
            return (from device in _dbContext.Devices
                    join deviceGroupDevice in _dbContext.DeviceGroupDevices
                    on device.DeviceReferenceId equals deviceGroupDevice.DeviceReferenceId
                    join deviceGroupUser in _dbContext.DeviceGroupUsers
                    on deviceGroupDevice.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                    where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                    where deviceGroupUser.UserReferenceId == userReferenceId
                    select device)
                    .ToList();
        }

        private string GetUserReferenceId()
        {
            return HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private bool IsInGroup(string sn)
        {
            var deviceGroupDeviceEntity = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                           join deviceGroupUser in _dbContext.DeviceGroupUsers
                                           on deviceGroupDevice.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                           join device in _dbContext.Devices
                                           on deviceGroupDevice.DeviceReferenceId equals device.DeviceReferenceId
                                           where deviceGroupUser.UserReferenceId == UserReferenceId
                                           where device.Sn == sn
                                           select deviceGroupDevice).FirstOrDefault();
            return deviceGroupDeviceEntity != null;
        }

        private async Task HttpSendFile(byte[] fileContent)
        {
            await using (Response.Body)
            {
                var hasSent = 0;
                while (hasSent < fileContent.Length)
                {
                    if (HttpContext.RequestAborted.IsCancellationRequested) break;
                    var sendLength = fileContent.Length - hasSent < 1024 ? fileContent.Length - hasSent : 1024;
                    var buffer = new ReadOnlyMemory<byte>(fileContent, hasSent, sendLength);
                    await Response.Body.WriteAsync(buffer);
                    hasSent += sendLength;
                }
            }
        }

        private bool UpdateDeviceAudio(int deviceAudioKey)
        {
            var deviceEntity = (from deviceAudio in _dbContext.DeviceAudios
                join device in _dbContext.Devices
                    on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                where deviceAudio.DeviceAudioKey == deviceAudioKey
                select device).FirstOrDefault();
            if (deviceEntity == null) return false;
            
            var deviceAudioEntity =
                (from deviceAudio in _dbContext.DeviceAudios
                    where deviceAudio.DeviceAudioKey == deviceAudioKey
                    select deviceAudio).FirstOrDefault();
            if (deviceAudioEntity == null) return false;
            
            var index =
                ((from deviceAudio in _dbContext.DeviceAudios
                    where deviceAudio.DeviceReferenceId == deviceEntity.DeviceReferenceId &&
                          deviceAudio.IsSynced == "Y"
                    orderby deviceAudio.Index descending
                    select deviceAudio.Index).FirstOrDefault() ?? 0) + 1;
            
            deviceAudioEntity.Index = index;
            deviceAudioEntity.IsSynced = "Y";
            _dbContext.DeviceAudios.Update(deviceAudioEntity);
            _dbContext.SaveChanges();
            return true;
        }
    }
}