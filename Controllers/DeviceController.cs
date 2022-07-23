using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Controllers.DTO;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
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

        private string UserReferenceId => GetUserReferenceId();

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
        [Authorize(Policy = "Permission")]
        [HttpPost("play_list")]
        public IActionResult GetPlayList([FromQuery] string sn)
        {
            if (!CheckPermission(sn, PermissionType.View)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
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
                var deviceAudioEntities =
                    (from deviceAudio in _dbContext.DeviceAudios
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
                            deviceAudio.AudioReferenceId,
                            deviceAudio.Index,
                            deviceAudio.IsSynced,
                            audio.AudioName,
                            audio.Size
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
                        deviceAudio.Index,
                        deviceAudio.IsSynced,
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
                var deviceAudioEntities = (from deviceAudio in _dbContext.DeviceAudios
                                           join device in _dbContext.Devices
                                           on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                                           join userDevice in _dbContext.UserDevices
                                           on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                           where userDevice.UserRefrenceId == UserReferenceId
                                           where device.Sn == request.Sn
                                           where deviceAudio.AudioReferenceId == request.AudioReferenceId
                                           select deviceAudio).FirstOrDefault();
                if (deviceAudioEntities != null)
                    return Ok();

                var entity = (from device in _dbContext.Devices
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

                if (entity?.Device == null || entity.Audio == null)
                    return BadRequest("参数有误");

                deviceAudioEntities = new DeviceAudio()
                {
                    DeviceReferenceId = entity.Device.DeviceReferenceId,
                    AudioReferenceId = entity.Audio.AudioReferenceId,
                    IsSynced = "N"
                };
                _dbContext.DeviceAudios.Add(deviceAudioEntities);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddDeviceAudios is invoked");
                return BadRequest(ex.Message);
            }
        }
        
        /// <summary>
        /// 获取设备组音频
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
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
                    if (_deviceContext.DevicePoolConCurrent.TryGetValue(deviceEntity.Sn, out var device))
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
        [Authorize(Policy = "Permission")]
        [HttpPost("delete_audio")]
        public IActionResult DeleteAudio([FromQuery] string sn, int index)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");

            if (IsInGroup(sn))
                return BadRequest("处于设备组的设备不可编辑音频");

            DeviceHandler deviceHandler = _deviceContext.DevicePoolConCurrent[sn];

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
        /// 删除设备音频
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="audioReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("remove_audio")]
        public IActionResult RemoveAudio([FromQuery] string sn, [FromQuery] string audioReferenceId)
        {
            try
            {
                var entity =
                    (from deviceAudio in _dbContext.DeviceAudios
                        join device in _dbContext.Devices
                            on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                        join audio in _dbContext.Audios
                            on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                        where device.Sn == sn &&
                              deviceAudio.AudioReferenceId == audioReferenceId
                        select new
                        {
                            device,
                            audio,
                            deviceAudio
                        }).FirstOrDefault();
                if (entity?.deviceAudio == null)
                    return BadRequest("找不到此记录，请刷新页面");
                if (entity.audio == null)
                    return BadRequest("找不到此音频");
                if (entity.device == null)
                    return BadRequest("找不到此设备");
            
                if (IsInGroup(entity.device.Sn))
                    return BadRequest("处于设备组的设备不可编辑音频");

                if (!_deviceContext.DevicePoolConCurrent.TryGetValue(entity.device.Sn, out var deviceHandler))
                    return BadRequest("设备未在线");

                // 删除已同步音频
                if (entity.deviceAudio.Index != null && entity.deviceAudio.IsSynced == "Y")
                {
                    var index = (int)entity.deviceAudio.Index;
                    var deviceRet = deviceHandler.DeleteAudio(index);
                    if (!deviceRet)
                        return BadRequest("设备未响应");
                
                    using var transaction = _dbContext.Database.BeginTransaction();
                    
                    _dbContext.DeviceAudios.Remove(entity.deviceAudio);
                    _dbContext.SaveChanges();
                    
                    var updatingDeviceAudioEntities =
                        (from deviceAudio in _dbContext.DeviceAudios
                            where deviceAudio.DeviceReferenceId == entity.device.DeviceReferenceId &&
                                  deviceAudio.IsSynced == "Y" &&
                                  deviceAudio.Index > index
                            orderby deviceAudio.Index
                            select deviceAudio).ToList();
                    if (updatingDeviceAudioEntities.Any())
                    {
                        updatingDeviceAudioEntities.ForEach(x => x.Index--);
                        _dbContext.DeviceAudios.UpdateRange(updatingDeviceAudioEntities);
                        _dbContext.SaveChanges();
                    }

                    transaction.Commit();
                }
                // 删除未同步音频
                else
                {
                    _dbContext.DeviceAudios.Remove(entity.deviceAudio);
                    _dbContext.SaveChanges();
                }

                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.DeviceControlApi, e, "While RemoveAudio is invoked");
                return BadRequest("删除失败");
            }
        }

        /// <summary>
        /// 设备组删除音频
        /// </summary>
        /// <param name="deviceGroupReferenceId"></param>
        /// <param name="audioReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("delete_audio_group")]
        public IActionResult DeleteAudioGroup([FromQuery] string deviceGroupReferenceId,
            [FromQuery] string audioReferenceId)
        {
            var errorCnt = 0;
            try
            {
                var deviceEntities =
                    (from device in _dbContext.Devices
                        join deviceGroupDevice in _dbContext.DeviceGroupDevices
                            on device.DeviceReferenceId equals deviceGroupDevice.DeviceReferenceId
                        where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                        select device).ToList();
                if (!deviceEntities.Any()) return BadRequest("没有设备组成员");

                foreach (var device in deviceEntities)
                {
                    if (!_deviceContext.DevicePoolConCurrent.TryGetValue(device.Sn, out var deviceHandler))
                    {
                        errorCnt++;
                        continue;
                    }
                    
                    var deviceAudioEntity =
                        (from deviceAudio in _dbContext.DeviceAudios
                            where deviceAudio.DeviceReferenceId == device.DeviceReferenceId &&
                                  deviceAudio.AudioReferenceId == audioReferenceId
                            select deviceAudio).FirstOrDefault();
                    if (deviceAudioEntity == null)
                    {
                        errorCnt++;
                        continue;
                    }

                    // 删除已同步音频
                    if (deviceAudioEntity.IsSynced == "Y" && deviceAudioEntity.Index != null)
                    {
                        var index = (int)deviceAudioEntity.Index;
                        var deviceRet = deviceHandler.DeleteAudio(index);
                        if (!deviceRet)
                        {
                            errorCnt++;
                            continue;
                        }

                        using var transaction = _dbContext.Database.BeginTransaction();

                        _dbContext.DeviceAudios.Remove(deviceAudioEntity);
                        _dbContext.SaveChanges();

                        var updatingDeviceAudios =
                            (from deviceAudio in _dbContext.DeviceAudios
                                where deviceAudio.DeviceReferenceId == device.DeviceReferenceId &&
                                      deviceAudio.IsSynced == "Y" &&
                                      deviceAudio.Index > index
                                      orderby deviceAudio.Index
                                select deviceAudio).ToList();
                        if (updatingDeviceAudios.Any())
                        {
                            updatingDeviceAudios.ForEach(x => x.Index--);
                            _dbContext.DeviceAudios.UpdateRange(updatingDeviceAudios);
                            _dbContext.SaveChanges();
                        }
                        
                        transaction.Commit();
                    }
                    // 删除未同步音频
                    else
                    {
                        _dbContext.DeviceAudios.Remove(deviceAudioEntity);
                        _dbContext.SaveChanges();
                    }
                }

                return Ok($"删除成功{deviceEntities.Count - errorCnt}个, 失败{errorCnt}个");
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.DeviceControlApi, e, "While DeleteAudioGroup is invoked");
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("play_index")]
        public IActionResult PlayIndex([FromQuery] string sn, int index)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");

            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
            return device.PlayIndex(index) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组播放指定音频
        /// </summary>
        /// <param name="deviceGroupReferenceId"></param>
        /// <param name="audioReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("play_audio_group")]
        public IActionResult PlayAudioGroup([FromQuery] string deviceGroupReferenceId,
            [FromQuery] string audioReferenceId)
        {
            var errorCnt = 0;
            try
            {
                var deviceEntities =
                    (from device in _dbContext.Devices
                        join deviceGroupDevice in _dbContext.DeviceGroupDevices on device.DeviceReferenceId equals
                            deviceGroupDevice.DeviceReferenceId
                        where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                        select device).ToList();
                if (!deviceEntities.Any()) return BadRequest("没有成员设备");

                foreach (var device in deviceEntities)
                {
                    var deviceAudioEntity =
                        (from deviceAudio in _dbContext.DeviceAudios
                            where deviceAudio.DeviceReferenceId == device.DeviceReferenceId
                                  && deviceAudio.AudioReferenceId == audioReferenceId
                                  && deviceAudio.IsSynced == "Y"
                            select deviceAudio).FirstOrDefault();
                    if (deviceAudioEntity == null)
                    {
                        errorCnt++;
                        continue;
                    }

                    var index = deviceAudioEntity.Index;
                    if (index == null)
                    {
                        errorCnt++;
                        continue;
                    }

                    if (!_deviceContext.DevicePoolConCurrent.TryGetValue(device.Sn, out var deviceHandler))
                    {
                        errorCnt++;
                        continue;
                    }

                    if (!deviceHandler.PlayIndex((int)index))
                    {
                        errorCnt++;
                    }
                }

                return Ok($"成功播放{deviceEntities.Count - errorCnt}个, 失败{errorCnt}个");
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.DeviceControlApi, e, "While PlayAudioGroup is invoked");
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// 播放或暂停
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 播放; 2: 暂停</param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("play_pause")]
        public IActionResult PlayOrPause([FromQuery] string sn, int action)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");

            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
            return device.SendPlayOrPause(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组控制播放暂停
        /// </summary>
        /// <param name="request">DeviceGroupReferenceId:String Action:Int(1:播放，2:暂停)</param>
        /// <returns></returns>
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
                    if (_deviceContext.DevicePoolConCurrent.TryGetValue(deviceEntity.Sn, out var device))
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
        [Authorize(Policy = "Permission")]
        [HttpPost("next_previous")]
        public IActionResult NextOrPrevious([FromQuery] string sn, int action)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            _logger.LogInformation("NextOrPrevious");
            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
            return device.SendNextOrPrevious(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组下一首或上一首
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("next_previous_group")]
        public IActionResult NextOrPreviousGroup([FromBody] NextOrPreviousGroupRequest request)
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

            var response = new NextOrPreviousGroupResponse();

            try
            {
                var deviceEntities = GetGroupDevices(request.DeviceGroupReferenceId, userReferenceId);
                deviceEntities.ForEach(deviceEntity =>
                {
                    if (_deviceContext.DevicePoolConCurrent.TryGetValue(deviceEntity.Sn, out var device))
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
        [Authorize(Policy = "Permission")]
        [HttpPost("volume")]
        public IActionResult Volume([FromQuery] string sn, int volume)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
            return device.SendVolume(volume) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组音量
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
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
                        if (_deviceContext.DevicePoolConCurrent.TryGetValue(deviceEntity.Sn, out var device))
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
        [Authorize(Policy = "Permission")]
        [HttpPost("reboot")]
        public IActionResult Reboot([FromQuery] string sn)
        {
            if (!CheckPermission(sn, PermissionType.Admin)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
            return device.SendReboot() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组重启
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
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
                    if (_deviceContext.DevicePoolConCurrent.TryGetValue(deviceEntity.Sn, out var device))
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
        [Authorize(Policy = "Permission")]
        [HttpPost("restore")]
        public IActionResult Restore([FromQuery] string sn)
        {
            if (!CheckPermission(sn, PermissionType.Admin)) return BadRequest("没有操作权限");
            DeviceHandler device = _deviceContext.DevicePoolConCurrent[sn];
            return device.SendRestore() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备组重置
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
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
                    if (_deviceContext.DevicePoolConCurrent.TryGetValue(deviceEntity.Sn, out var device))
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
        [Authorize(Policy = "Permission")]
        [HttpPost("cron_tasks")]
        public IActionResult SetCronTasks([FromQuery] string sn, [FromBody] IList<CronTaskDto> dtos)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            var device = _deviceContext.DevicePoolConCurrent[sn];

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

                if (!string.IsNullOrEmpty(dto.AudioReferenceId))
                {
                    var audioIndex = (from deviceAudio in _dbContext.DeviceAudios
                        join deviceEntity in _dbContext.Devices
                            on deviceAudio.DeviceReferenceId equals deviceEntity.DeviceReferenceId
                        where deviceAudio.AudioReferenceId == dto.AudioReferenceId
                              && deviceEntity.Sn == sn
                              && deviceAudio.IsSynced == "Y"
                        select deviceAudio.Index).FirstOrDefault();
                    if (audioIndex == null) continue;
                    data.Add((byte)audioIndex);
                }
                else if (dto.Audio != null)
                {
                    data.Add((byte)dto.Audio);
                }
                else continue;
                
                dto.Weekdays.ForEach(d => { data.Add((byte)(d + 1)); });
                if (!device.SendCronTask(data)) return BadRequest("设置失败");
            }

            return Ok();
        }

        /// <summary>
        /// 设备组下发定时任务
        /// </summary>
        /// <param name="deviceGroupReferenceId"></param>
        /// <param name="dtos"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("cron_task_group")]
        public IActionResult SetCronTaskGroup([FromQuery] string deviceGroupReferenceId,
            [FromBody] IList<CronTaskDto> dtos)
        {
            var deviceErrorCnt = 0;
            var taskErrorCnt = 0;
            
            var deviceEntities =
                (from device in _dbContext.Devices
                    join deviceGroupDevice in _dbContext.DeviceGroupDevices 
                        on device.DeviceReferenceId equals deviceGroupDevice.DeviceReferenceId
                    where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                    select device).ToList();
            if (!deviceEntities.Any()) return BadRequest("该组没有成员设备");

            foreach (var device in deviceEntities)
            {
                if (!_deviceContext.DevicePoolConCurrent.TryGetValue(device.Sn, out var deviceHandler))
                {
                    deviceErrorCnt++;
                    continue;
                }

                if (!deviceHandler.SendCronTaskCount(dtos.Count))
                {
                    deviceErrorCnt++;
                    continue;
                }

                foreach (var taskDto in dtos)
                {
                    List<byte> data = new()
                    {
                        (byte) taskDto.Index,
                        (byte) taskDto.StartTime.Hour,
                        (byte) taskDto.StartTime.Minute,
                        (byte) taskDto.EndTime.Hour,
                        (byte) taskDto.EndTime.Minute,
                        (byte) taskDto.Volume,
                        (byte) (taskDto.Relay ? 0x01 : 0x00)
                    };
                    if (string.IsNullOrEmpty(taskDto.AudioReferenceId))
                    {
                        taskErrorCnt++;
                        continue;
                    }
                    var audioIndex = 
                        (from deviceAudio in _dbContext.DeviceAudios
                        where deviceAudio.AudioReferenceId == taskDto.AudioReferenceId
                              && deviceAudio.DeviceReferenceId == device.DeviceReferenceId
                              && deviceAudio.IsSynced == "Y"
                        select deviceAudio.Index).FirstOrDefault();
                    if (audioIndex == null)
                    {
                        taskErrorCnt++;
                        continue;
                    }
                    data.Add((byte) audioIndex);
                    taskDto.Weekdays.ForEach(d => { data.Add((byte)(d + 1)); });
                    if (!deviceHandler.SendCronTask(data)) deviceErrorCnt++;
                }
            }

            if (deviceErrorCnt == 0 && taskErrorCnt == 0)
                return Ok("设置成功");
            if (deviceErrorCnt == 0)
                return Ok($"部分成功. {taskErrorCnt}个任务设置失败");
            if (taskErrorCnt == 0)
                return Ok($"部分成功. {deviceEntities}个设备无响应");
            return Ok($"部分成功. {taskErrorCnt}个任务设置失败, {deviceEntities}个设备无响应");
        }

        /// <summary>
        /// 下发延时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("delay_task")]
        public IActionResult SetAlarmsAfter([FromQuery] string sn, [FromBody] DelayTaskDto dto)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            var device = _deviceContext.DevicePoolConCurrent[sn];

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
        /// 查看设备定时开关状态
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("check_task_status")]
        public IActionResult CheckTaskStatus([FromQuery] string sn)
        {
            if (!_deviceContext.DevicePoolConCurrent.TryGetValue(sn, out var deviceHandler))
                return BadRequest("设备不在线");

            return Ok(deviceHandler.SendCheckTaskStatus());
        }

        /// <summary>
        /// 查看设备组定时开关状态
        /// </summary>
        /// <param name="deviceGroupReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("check_task_status_group")]
        public IActionResult CheckTaskStatusGroup([FromQuery] string deviceGroupReferenceId)
        {
            var deviceEntities =
                (from device in _dbContext.Devices
                    join deviceGroupDevice in _dbContext.DeviceGroupDevices
                        on device.DeviceReferenceId equals deviceGroupDevice.DeviceReferenceId
                    where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                    select device).ToList();
            if (!deviceEntities.Any())
                return Ok();

            var results = new List<bool>();
            foreach (var device in deviceEntities)
            {
                if (!_deviceContext.DevicePoolConCurrent.TryGetValue(device.Sn, out var deviceHandler))
                    continue;
                results.Add(deviceHandler.SendCheckTaskStatus());
            }

            if (results.All(x => x))
                return Ok("全部打开");
            if (results.All(x => !x))
                return Ok("全部关闭");
            return Ok("部分打开");
        }

        /// <summary>
        /// 设置设备定时状态
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("set_task_status")]
        public IActionResult SetTaskStatus([FromQuery] string sn, [FromQuery] bool status)
        {
            if (!_deviceContext.DevicePoolConCurrent.TryGetValue(sn, out var deviceHandler))
                return BadRequest("设备不在线");

            if (status)
                return deviceHandler.SendOpenTask() ? Ok() : BadRequest("设备未响应");
            return deviceHandler.SendCloseTask() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设置设备组定时状态
        /// </summary>
        /// <param name="deviceGroupReferenceId"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("set_task_status_group")]
        public IActionResult SetTaskStatusGroup([FromQuery] string deviceGroupReferenceId, bool status)
        {
            var errorCnt = 0;
            var deviceEntities =
                (from device in _dbContext.Devices
                    join deviceGroupDevice in _dbContext.DeviceGroupDevices
                        on device.DeviceReferenceId equals deviceGroupDevice.DeviceReferenceId
                    where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                    select device).ToList();
            if (!deviceEntities.Any())
                return Ok("设备组没有成员设备");

            foreach (var device in deviceEntities)
            {
                if (!_deviceContext.DevicePoolConCurrent.TryGetValue(device.Sn, out var deviceHandler))
                {
                    errorCnt++;
                    continue;
                }

                if (status)
                {
                    if (!deviceHandler.SendOpenTask())
                        errorCnt++;
                }
                else
                {
                    if (!deviceHandler.SendCloseTask())
                        errorCnt++;
                }
            }

            return Ok($"成功{deviceEntities.Count - errorCnt}个，失败{errorCnt}个");
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("file/sn{sn}")]
        public async Task<IActionResult> TransFile(string sn, IFormFile formFile)
        {
            if (!CheckPermission(sn, PermissionType.Control)) return BadRequest("没有操作权限");
            // 读取文件到内存
            var content = new byte[formFile.Length];
            _ = await formFile.OpenReadStream().ReadAsync(content);
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

            var device = _deviceContext.DevicePoolConCurrent[sn];
            // WiFi 设备传输
            if (device.Type == Nsb.Type.DeviceType.WiFi_Test)
            {
                var fileUploadHandle = new Services.Message.File(content.ToList());
                device.FileQueue.Add(fileUploadHandle);
                fileUploadHandle.Semaphore.WaitOne();
                return fileUploadHandle.FileStatus == FileStatus.Success ? Ok() : BadRequest("文件传输失败");
            }
            
            // 4G 设备传输
            AudioTrxModel dto = new()
            {
                FileName = fileName,
                AudioPath = fullPath,
                Sn = sn
            };
            var fileToken = Guid.NewGuid().ToString("N")[..8];
            _deviceContext.AudioDict.Add(fileToken, dto);
            if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");
            return await dto.Wait() ? Ok() : BadRequest("下载超时");
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
            var device = _deviceContext.DevicePoolConCurrent[sn];
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
                        AudioPath = fullPath,
                        Sn = sn
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

        /// <summary>
        /// 设备调用 流式传输文件
        /// </summary>
        /// <param name="fileToken"></param>
        /// <returns></returns>
        [HttpGet("download_file_stream")]
        public async Task<IActionResult> DownloadFileStreamNew([FromQuery] string fileToken)
        {
            if (!_deviceContext.AudioDict.TryGetValue(fileToken, out var audioHandler))
                return new EmptyResult();
            var result = false;
            try
            {
                var fileContent = await System.IO.File.ReadAllBytesAsync($"{audioHandler.AudioPath}");

                var contentDisposition = $"attachment;filename={DateTime.UtcNow.Ticks.ToString()}.mp3";
                Response.Headers.Add("Content-Disposition", new[] { contentDisposition });
                Response.ContentType = "audio/mp3";
                Response.ContentLength = fileContent.Length;

                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                result = audioHandler.DeviceAudioKey != null ? 
                    UpdateDeviceAudioRecord((int)audioHandler.DeviceAudioKey) : 
                    AddDeviceAudioRecord(audioHandler.Sn);
                
                if (!result) return new EmptyResult();

                await using (Response.Body)
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    var hasSent = 0;
                    while (hasSent < fileContent.Length)
                    {
                        if (HttpContext.RequestAborted.IsCancellationRequested) break;
                        var sendLength = fileContent.Length - hasSent < 1024 ? fileContent.Length - hasSent : 1024;
                        var buffer = new ReadOnlyMemory<byte>(fileContent, hasSent, sendLength);
                        await Response.Body.WriteAsync(buffer);
                        hasSent += sendLength;
                    }
                    sw.Stop();
                    _logger.LogInformation(LogEvent.DeviceControlApi, $"Download File Complete, time elapsed: {sw.ElapsedMilliseconds}ms");
                }

                await transaction.CommitAsync();
                _logger.LogInformation(LogEvent.DeviceControlApi, $"Update records in DB");
                await _notificationContext.SendAudioSyncComplete(audioHandler.Sn,
                    audioHandler.AudioReferenceId);
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

        /// <summary>
        /// 同步音频文件
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("sync_device_audio")]
        public IActionResult SyncDeviceAudio([FromQuery] string sn)
        {
            if (string.IsNullOrEmpty(sn)) return BadRequest("请求为空");

            var entities =
                (from deviceAudio in _dbContext.DeviceAudios
                    join audio in _dbContext.Audios
                        on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                    join device in _dbContext.Devices
                        on deviceAudio.DeviceReferenceId equals  device.DeviceReferenceId
                    where device.Sn == sn && 
                          deviceAudio.IsSynced == "N"
                    select new
                    {
                        DeviceAudio = deviceAudio,
                        Audio = audio,
                        Device = device
                    }).ToList();
            if (!entities.Any()) return BadRequest("没有未同步音频");
            
            entities.ForEach(entity =>
            {
                _helper.AddAudioSyncEvent(new AudioSyncEvent()
                {
                    AudioPath = $"{_audioRootPath}{entity.Audio.AudioPath}",
                    DeviceAudioKey = entity.DeviceAudio.DeviceAudioKey,
                    DeviceReferenceId = entity.Device.DeviceReferenceId,
                    AudioReferenceId = entity.Audio.AudioReferenceId,
                    FileName = entity.Audio.AudioName,
                    OperationType = OperationType.Add,
                    Sn = entity.Device.Sn
                });
            });
            return Ok();
        }

        #region Private Function

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

        private bool UpdateDeviceAudioRecord(int deviceAudioKey)
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
        
        private bool AddDeviceAudioRecord(string sn)
        {
            var deviceEntity =
                (from device in _dbContext.Devices
                    where device.Sn == sn
                    select device).FirstOrDefault();
            if (deviceEntity == null)
                return false;

            var index =
                ((from deviceAudio in _dbContext.DeviceAudios
                    where deviceAudio.DeviceReferenceId == deviceEntity.DeviceReferenceId &&
                          deviceAudio.IsSynced == "Y" &&
                          deviceAudio.Index != null
                    orderby deviceAudio.Index descending
                    select deviceAudio.Index).FirstOrDefault() ?? 0) + 1;

            var deviceAudioEntity = new DeviceAudio()
            {
                DeviceReferenceId = deviceEntity.DeviceReferenceId,
                AudioReferenceId = null,
                IsSynced = "Y",
                Index = index
            };

            _dbContext.DeviceAudios.Add(deviceAudioEntity);
            _dbContext.SaveChanges();
            return true;
        }

        #endregion
    }
}