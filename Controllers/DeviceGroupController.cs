using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Controllers.Model.Request;
using NetworkSoundBox.Controllers.Model.Response;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Logger;
using NetworkSoundBox.Models;
using NetworkSoundBox.Services.Audios;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace NetworkSoundBox.Controllers
{
    [Route("api/device_group")]
    [ApiController]
    public class DeviceGroupController : ControllerBase
    {
        private readonly MySqlDbContext _dbContext;
        private readonly ILogger<DeviceGroupController> _logger;
        private readonly IMapper _mapper;
        private readonly IAudioProcessorHelper _helper;


        public DeviceGroupController(
            MySqlDbContext dbContext,
            ILogger<DeviceGroupController> logger,
            IMapper mapper,
            IAudioProcessorHelper helper)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mapper = mapper;
            _helper = helper;
        }

        /// <summary>
        /// 创建设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_group")]
        public IActionResult CreateDeviceGroup([FromBody] CreateDeviceGroupRequest request)
        {
            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                if (string.IsNullOrEmpty(request?.Name))
                {
                    request.Name = new Guid().ToString("N")[..8];
                }

                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         where deviceGroup.Name.Equals(request.Name)
                                         select deviceGroup)
                                         .FirstOrDefault();
                if (deviceGroupEntity != null)
                {
                    return BadRequest("该名称已占用");
                }

                var deviceGroupReferenceId = Guid.NewGuid().ToString();
                _dbContext.DeviceGroups.Add(new DeviceGroup()
                {
                    Name = request.Name,
                    DeviceGroupReferenceId = deviceGroupReferenceId,
                    UsingStatus = 1
                });
                _dbContext.DeviceGroupUsers.Add(new DeviceGroupUser()
                {
                    DeviceGroupReferenceId = deviceGroupReferenceId,
                    UserReferenceId = userReferenceId
                });
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While CreateDeviceGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 删除设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("del_group")]
        public IActionResult RemoveDeviceGroup([FromBody] RemoveDeviceGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId))
            {
                return BadRequest("请求为空");
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                         select deviceGroup)
                                     .FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
                }

                var deviceGroupDeviceEntities = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                                 where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupEntity.DeviceGroupReferenceId
                                                 select deviceGroupDevice)
                                             ?.ToList();
                if (deviceGroupDeviceEntities != null && deviceGroupDeviceEntities.Any())
                {
                    _dbContext.DeviceGroupDevices.RemoveRange(deviceGroupDeviceEntities);
                }

                var deviceGroupUserEntities = (from deviceGroupUser in _dbContext.DeviceGroupUsers
                                               where deviceGroupUser.DeviceGroupReferenceId == deviceGroupEntity.DeviceGroupReferenceId
                                               select deviceGroupUser)
                                               ?.ToList();
                if (deviceGroupUserEntities != null && deviceGroupUserEntities.Any())
                {
                    _dbContext.DeviceGroupUsers.RemoveRange(deviceGroupUserEntities);
                }

                _dbContext.DeviceGroups.Remove(deviceGroupEntity);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While RemoveDeviceGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 更新设备组信息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("update_group")]
        public IActionResult UpdateDeviceGroup([FromBody] UpdateDeviceGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId) ||
                string.IsNullOrEmpty(request?.DeviceGroupName))
            {
                return BadRequest("请求为空");
            }

            try
            {
                var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
                if (string.IsNullOrEmpty(userReferenceId))
                {
                    return Unauthorized();
                }

                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         select deviceGroup)
                                         .FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
                }

                if (_dbContext.DeviceGroups.Where(device => 
                            device.Name == request.DeviceGroupName && 
                            device.DeviceGroupReferenceId != deviceGroupEntity.DeviceGroupReferenceId).Any())
                {
                    return BadRequest("名称已占用");
                }

                deviceGroupEntity.Name = request.DeviceGroupName;
                _dbContext.DeviceGroups.Update(deviceGroupEntity);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While UpdateDeviceGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 添加设备到设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_device")]
        public IActionResult AddDeviceToGroup([FromBody] AddDeviceToGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceID) ||
                string.IsNullOrEmpty(request?.DeviceGroupReferenceID))
            {
                return BadRequest("请求为空");
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceID
                                         select deviceGroup)
                                            .FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceID}");
                }

                var deviceEntity = (from device in _dbContext.Devices
                                    join userDevice in _dbContext.UserDevices
                                    on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                    where userDevice.UserRefrenceId == userReferenceId
                                    where device.DeviceReferenceId == request.DeviceReferenceID
                                    select device)
                                    .FirstOrDefault();
                if (deviceEntity == null)
                {
                    return NotFound($"找不到设备{request.DeviceReferenceID}");
                }

                if (_dbContext.DeviceGroupDevices.Where(dgd => 
                        dgd.DeviceReferenceId == request.DeviceReferenceID &&
                        dgd.DeviceGroupReferenceId == request.DeviceGroupReferenceID).Any())
                {
                    return Ok();
                }

                _dbContext.DeviceGroupDevices.Add(new DeviceGroupDevice()
                {
                    DeviceGroupReferenceId = request.DeviceGroupReferenceID,
                    DeviceReferenceId = request.DeviceReferenceID
                });
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While AddDeviceToGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 添加设备列表到设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_devices")]
        public IActionResult AddDevicesToGroup([FromBody] AddDevicesToGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId) ||
                !request.Devices.Any())
            {
                return BadRequest();
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroup.DeviceGroupReferenceId.Equals(request.DeviceGroupReferenceId)
                                         where deviceGroupUser.UserReferenceId.Equals(userReferenceId)
                                         select deviceGroup)
                                             ?.FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
                }

                var response = new AddDevicesToGroupResponse();
                var deviceEntities = new List<Device>();
                request.Devices.ForEach(deviceReferenceId =>
                {
                    var deviceEntity = (from d in _dbContext.Devices
                                        join userDevice in _dbContext.UserDevices
                                        on d.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                        where d.DeviceReferenceId == deviceReferenceId
                                        where userDevice.UserRefrenceId == userReferenceId
                                        select d)
                                       .FirstOrDefault();
                    if (deviceEntity == null)
                    {
                        response.SkippedCount++;
                        response.SkippedDevices.Add(deviceReferenceId);
                    }
                    else
                    {
                        response.SuccessCount++;
                        deviceEntities.Add(deviceEntity);
                    }
                });

                deviceEntities.ForEach(d =>
                {
                    if (!_dbContext.DeviceGroupDevices.Where(dgd => 
                            dgd.DeviceReferenceId == d.DeviceReferenceId && 
                            dgd.DeviceGroupReferenceId == request.DeviceGroupReferenceId).Any())
                    {
                        _dbContext.DeviceGroupDevices.Add(new DeviceGroupDevice()
                        {
                            DeviceReferenceId = d.DeviceReferenceId,
                            DeviceGroupReferenceId = request.DeviceGroupReferenceId
                        });
                    }
                });
                _dbContext.SaveChanges();
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While AddDevicesToGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 从设备组移除设备列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("remove_devices")]
        public ActionResult<RemoveDevicesFromGroupResponse> RemoveDevicesFromGroup([FromBody] RemoveDevicesFromGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId) ||
                request.Devices == null ||
                !request.Devices.Any())
            {
                return BadRequest("请求为空");
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         select deviceGroup)?.FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
                }

                var response = new RemoveDevicesFromGroupResponse();
                request.Devices.ForEach(d =>
                {
                    var deviceGroupDevicveEntity = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                                    where deviceGroupDevice.DeviceReferenceId == d
                                                    select deviceGroupDevice)
                                                    .FirstOrDefault();
                    if (deviceGroupDevicveEntity == null)
                    {
                        response.SkippedDevices.Add(d);
                        response.SkippedCount++;
                    }
                    else
                    {
                        response.SuccessCount++;
                        _dbContext.DeviceGroupDevices.Remove(deviceGroupDevicveEntity);
                    }
                });
                _dbContext.SaveChanges();
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While RemoveDevicesFromGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 从设备组移除设备
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("remove_device")]
        public IActionResult RemoveDeviceFromDeviceGroup([FromBody] RemoveDeviceFromGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId) ||
                string.IsNullOrEmpty(request?.DeviceReferenceId))
            {
                return BadRequest("请求为空");
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         select deviceGroup)
                                             ?.FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
                }

                var deviceGroupDeviceEntity = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                               where deviceGroupDevice.DeviceReferenceId == request.DeviceReferenceId
                                               where deviceGroupDevice.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                               select deviceGroupDevice)
                                               .FirstOrDefault();
                if (deviceGroupDeviceEntity == null)
                {
                    return Ok();
                }

                _dbContext.DeviceGroupDevices.Remove(deviceGroupDeviceEntity);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While RemoveDeviceFromDeviceGroup is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 更新设备组设备列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("update_devices")]
        public IActionResult UpdateDeviceGroupDevices([FromBody] UpdateDeviceGroupDevicesRequest request)
        {
            if (request?.DeviceReferenceIds == null ||
                string.IsNullOrEmpty(request.DeviceGroupReferenceId))
            {
                return BadRequest("请求为空");
            }

            try
            {
                var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
                if (string.IsNullOrEmpty(userReferenceId))
                {
                    return Unauthorized();
                }

                var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                         join deviceGroupUser in _dbContext.DeviceGroupUsers
                                         on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                         where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                         where deviceGroupUser.UserReferenceId == userReferenceId
                                         select deviceGroup)
                                         .FirstOrDefault();
                if (deviceGroupEntity == null)
                {
                    return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
                }

                var response = new UpdateDeviceGroupDevicesResponse();

                var currentDeviceGroupDeviceEntities = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                                        where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupEntity.DeviceGroupReferenceId
                                                        select deviceGroupDevice)
                                                        .ToList();

                var deviceEntities = (from device in _dbContext.Devices
                                      join userDevice in _dbContext.UserDevices
                                      on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                      where userDevice.UserRefrenceId == userReferenceId
                                      where request.DeviceReferenceIds.Contains(device.DeviceReferenceId)
                                      select device)
                                      .ToList();

                _dbContext.DeviceGroupDevices.RemoveRange(currentDeviceGroupDeviceEntities);
                deviceEntities.ForEach(device =>
                {
                    _dbContext.DeviceGroupDevices.Add(new DeviceGroupDevice()
                    {
                        DeviceReferenceId = device.DeviceReferenceId,
                        DeviceGroupReferenceId = request.DeviceGroupReferenceId
                    });
                });
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While UpdateDeviceGroupDevices is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 获取设备组列表
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("get_group")]
        public IActionResult GetDeviceGroupsByUser()
        {
            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Unauthorized();
            }

            try
            {
                var deviceGroupEntities = (from deviceGroup in _dbContext.DeviceGroups
                                           join deviceGroupUser in _dbContext.DeviceGroupUsers
                                           on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                           where deviceGroupUser.UserReferenceId == userReferenceId
                                           select deviceGroup)
                                               ?.ToList();
                var response = new List<GetDeviceGroupsByUserResponse>();
                foreach (var deviceGroup in deviceGroupEntities)
                {
                    int count = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                 where deviceGroupDevice.DeviceGroupReferenceId == deviceGroup.DeviceGroupReferenceId
                                 select deviceGroupDevice)
                                 .Count();
                    response.Add(new GetDeviceGroupsByUserResponse()
                    {
                        DeviceGroupReferenceId = deviceGroup.DeviceGroupReferenceId,
                        Name = deviceGroup.Name,
                        Count = count,
                        CreateTime = deviceGroup.CreatedAt.ToString("G"),
                        UpdateTime = deviceGroup.UpdatedAt.ToString("G"),
                    });
                }
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While GetDeviceGroupsByUser is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("get_group_audio")]
        public IActionResult GetDeviceGroupAudios([FromQuery]string deviceGroupReferenceId)
        {
            var deviceReferenceIds = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                      where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupReferenceId
                                      select deviceGroupDevice.DeviceReferenceId).ToList();
            if (!deviceReferenceIds.Any()) return BadRequest("设备组没有成员");
            var deviceListAudioList = new List<List<string>>();
            deviceReferenceIds.ForEach(deviceReferenceId =>
            {
                var audioList = (from deviceAudio in _dbContext.DeviceAudios
                                 join device in _dbContext.Devices
                                 on deviceAudio.DeviceReferenceId equals device.DeviceReferenceId
                                 join audio in _dbContext.Audios
                                 on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                                 where device.DeviceReferenceId == deviceReferenceId
                                 select deviceAudio.AudioReferenceId).ToList();
                deviceListAudioList.Add(audioList);
            });

            var intersectAudios = deviceListAudioList.FirstOrDefault();
            deviceListAudioList.Skip(1).ToList().ForEach(list =>
            {
                intersectAudios = intersectAudios.Intersect(list).ToList();
            });

            var responseList = new List<DeviceAudioDto>();
            intersectAudios.ForEach(audioReferenceId =>
            {
                var deviceAudioDtos = new List<DeviceAudioDto>();
                deviceReferenceIds.ForEach(deviceReferenceId =>
                {
                    var dto = (from deviceAudio in _dbContext.DeviceAudios
                               join audio in _dbContext.Audios
                               on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                               where deviceAudio.AudioReferenceId == audioReferenceId &&
                               deviceAudio.DeviceReferenceId == deviceReferenceId
                               select new DeviceAudioDto
                               {
                                   AudioReferenceId = audioReferenceId,
                                   IsSynced = deviceAudio.IsSynced,
                                   AudioName = audio.AudioName,
                                   Size = audio.Size
                               }).FirstOrDefault();
                    deviceAudioDtos.Add(dto);
                });
                var responseDto = new DeviceAudioDto()
                {
                    AudioReferenceId = deviceAudioDtos.FirstOrDefault().AudioReferenceId,
                    IsSynced = deviceAudioDtos.All(d => d.IsSynced == "Y") ? "Y" : "N",
                    AudioName = deviceAudioDtos.FirstOrDefault().AudioName,
                    Size = deviceAudioDtos.FirstOrDefault().Size
                };
                responseList.Add(responseDto);
            });

            return Ok(JsonConvert.SerializeObject(responseList));
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("add_group_audio")]
        public IActionResult AddGroupAudio([FromQuery]string deviceGroupReferenceId, [FromQuery]string audioReferenceId)
        {
            var deviceEntities = (from device in _dbContext.Devices
                                  join deviceGroup in _dbContext.DeviceGroupDevices
                                  on device.DeviceReferenceId equals deviceGroup.DeviceReferenceId
                                  where deviceGroup.DeviceGroupReferenceId == deviceGroupReferenceId
                                  select device).ToList();
            if (!deviceEntities.Any()) return BadRequest("没有成员设备");

            var audioEntity = (from audio in _dbContext.Audios
                               where audio.AudioReferenceId == audioReferenceId 
                               select audio).FirstOrDefault();
            if (audioEntity == null) return BadRequest("请求有误");

            deviceEntities.ForEach(device =>
            {
                var deviceAudioEntity = (from deviceAudio in _dbContext.DeviceAudios
                                         where deviceAudio.DeviceReferenceId == device.DeviceReferenceId
                                         && deviceAudio.AudioReferenceId == audioReferenceId
                                         select deviceAudio).FirstOrDefault();
                if (deviceAudioEntity == null)
                {
                    deviceAudioEntity = new DeviceAudio()
                    {
                        DeviceReferenceId = device.DeviceReferenceId,
                        AudioReferenceId = audioReferenceId,
                        IsSynced = "N"
                    };
                    _dbContext.DeviceAudios.Add(deviceAudioEntity);
                }
            });
            _dbContext.SaveChanges();
            return Ok();
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("sync_group_audio")]
        public IActionResult SyncGroupAudio([FromQuery]string deviceGroupReferenceId)
        {
            var deviceEntities = (from device in _dbContext.Devices
                                  join deviceGroup in _dbContext.DeviceGroupDevices
                                  on device.DeviceReferenceId equals deviceGroup.DeviceReferenceId
                                  where deviceGroup.DeviceGroupReferenceId == deviceGroupReferenceId
                                  select device).ToList();
            if (!deviceEntities.Any()) return BadRequest("没有成员设备");

            deviceEntities.ForEach(deviceEntity =>
            {
                var deviceAudioEntities =
                (from deviceAudio in _dbContext.DeviceAudios
                 join audio in _dbContext.Audios
                     on deviceAudio.AudioReferenceId equals audio.AudioReferenceId
                 where deviceAudio.DeviceReferenceId == deviceEntity.DeviceReferenceId &&
                       deviceAudio.IsSynced == "N"
                 select new
                 {
                     DeviceAudio = deviceAudio,
                     Audio = audio,
                     Device = deviceEntity
                 }).ToList();
                if (deviceAudioEntities.Any())
                {
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
                }
            });
            return Ok();
        }
        
        /// <summary>
        /// 获取设备组设备及未分组设备
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("get_device")]
        public ActionResult<GetDeviceGroupDevicesResponse> GetDeviceGroupDevices([FromBody] GetDeviceGroupDevicesReqeust request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId))
            {
                return BadRequest();
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Unauthorized();
            }

            var response = new GetDeviceGroupDevicesResponse();

            try
            {
                var deviceEntityFromGroup = (from device in _dbContext.Devices
                                             join deviceGroupDevice in _dbContext.DeviceGroupDevices
                                             on device.DeviceReferenceId equals deviceGroupDevice.DeviceReferenceId
                                             where deviceGroupDevice.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                             select device)
                                                 .ToList();
                response.DeviceFromGroup = _mapper.Map<List<Device>, List<DeviceModel>>(deviceEntityFromGroup);

                if (request.IsIncludeOtherDevice.HasValue && request.IsIncludeOtherDevice.Value)
                {
                    var deviceEntityExcluded = (from device in _dbContext.Devices
                                                join userDevice in _dbContext.UserDevices
                                                on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                                where userDevice.UserRefrenceId == userReferenceId
                                                select device)
                                                .Where(d => !deviceEntityFromGroup.Contains(d)).ToList();
                    response.DeviceExcluded = _mapper.Map<List<Device>, List<DeviceModel>>(deviceEntityExcluded);
                }
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceGroupApi, ex, "While GetDeviceGroupDevices is invoked");
                return BadRequest(ex.Message);
            }
        }
    }
}
