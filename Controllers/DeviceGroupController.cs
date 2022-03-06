using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Controllers.Model;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Logger;
using NetworkSoundBox.Models;
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

        public DeviceGroupController(
            MySqlDbContext dbContext,
            ILogger<DeviceGroupController> logger,
            IMapper mapper)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mapper = mapper;
        }

        /// <summary>
        /// 创建设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_group")]
        public IActionResult CreateDeviceGroup([FromBody]CreateDeviceGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.Name))
            {
                request.Name = new Guid().ToString("N")[..8];
            }

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return BadRequest();
            }

            var userEntity = (from user in _dbContext.Users
                              where user.UserRefrenceId == userReferenceId
                              select user)
                              .FirstOrDefault();
            if (userEntity == null)
            {
                return NotFound();
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
                return BadRequest();
            }

            var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                     where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                     select deviceGroup)
                                     .FirstOrDefault();
            if (deviceGroupEntity == null)
            {
                return NotFound();
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
            string message = $"移除设备组 DeviceGroupReference id: {deviceGroupEntity}," +
                $"设备组包含{deviceGroupUserEntities.Count}个设备";
            _logger.LogInformation(LogEvent.DeviceGroupApi,
                message);
            return Ok();
        }

        /// <summary>
        /// 添加设备到设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_device")]
        public IActionResult AddDeviceToGroup([FromBody]AddDeviceToGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceID) ||
                string.IsNullOrEmpty(request?.DeviceGroupReferenceID))
            {
                return BadRequest();
            }

            var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                     where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceID
                                     select deviceGroup)
                                    .FirstOrDefault();

            var deviceEntity = (from device in _dbContext.Devices
                                where device.DeviceReferenceId == request.DeviceReferenceID
                                select device)
                                .FirstOrDefault();

            if (deviceGroupEntity == null || deviceEntity == null)
            {
                return NotFound();
            }

            _dbContext.DeviceGroupDevices.Add(new DeviceGroupDevice()
            {
                DeviceGroupReferenceId = request.DeviceGroupReferenceID,
                DeviceReferenceId = request.DeviceReferenceID
            });
            _dbContext.SaveChanges(true);
            return Ok();
        }


        /// <summary>
        /// 添加设备列表到设备组
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_devices")]
        public IActionResult AddDevicesToGroup([FromBody]AddDevicesToGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId) || 
                !request.Devices.Any())
            {
                return BadRequest();
            }

            var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                     where deviceGroup.DeviceGroupReferenceId.Equals(request.DeviceGroupReferenceId)
                                     select deviceGroup)
                                     ?.FirstOrDefault();
            if (deviceGroupEntity == null)
            {
                return NotFound();
            }

            var response = new AddDevicesToGroupResponse();
            var deviceEntities = new List<Device>();
            request.Devices.ForEach(deviceReferenceId =>
            {
                var deviceEntity = (from d in _dbContext.Devices
                                    where d.DeviceReferenceId == deviceReferenceId
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

            var deviceGroupDeviceEntities = new List<DeviceGroupDevice>();
            deviceEntities.ForEach(d =>
            {
                deviceGroupDeviceEntities.Add(new DeviceGroupDevice()
                {
                    DeviceReferenceId = d.DeviceReferenceId,
                    DeviceGroupReferenceId = request.DeviceGroupReferenceId
                });
            });
            _dbContext.DeviceGroupDevices.AddRange(deviceGroupDeviceEntities);
            _dbContext.SaveChanges();
            return Ok(JsonConvert.SerializeObject(response));
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
                return BadRequest();
            }

            var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                     where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                     select deviceGroup)?.FirstOrDefault();
            if (deviceGroupEntity == null)
            {
                return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
            }

            var response = new RemoveDevicesFromGroupResponse();
            var removingDeviceGroupDeviceEntities = new List<DeviceGroupDevice>();
            request.Devices.ForEach(d =>
            {
                var deviceGroupDeviceEntity = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                         where deviceGroupDevice.DeviceReferenceId == d
                                         select deviceGroupDevice).FirstOrDefault();
                if (deviceGroupDeviceEntity == null)
                {
                    response.SkippedDevices.Add(d);
                    response.SkippedCount++;
                }
                else
                {
                    response.SuccessCount++;
                    removingDeviceGroupDeviceEntities.Add(deviceGroupDeviceEntity);
                }
            });
            if (removingDeviceGroupDeviceEntities.Any())
            {
                _dbContext.DeviceGroupDevices.RemoveRange(removingDeviceGroupDeviceEntities);
                _dbContext.SaveChanges();
            }
            return Ok(JsonConvert.SerializeObject(response));
        }

        /// <summary>
        /// 从设备组移除设备
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("remove_device")]
        public IActionResult RemoveDeviceFromDeviceGroup([FromBody]RemoveDeviceFromGroupRequest request)
        {
            if (string.IsNullOrEmpty(request?.DeviceGroupReferenceId) ||
                string.IsNullOrEmpty(request?.DeviceReferenceId))
            {
                return BadRequest();
            }

            var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                     where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                     select deviceGroup)
                                     ?.FirstOrDefault();
            if (deviceGroupEntity == null)
            {
                return NotFound();
            }

            var deviceEntity = (from device in _dbContext.Devices
                                where device.DeviceReferenceId == request.DeviceReferenceId
                                select device)
                                ?.FirstOrDefault();
            if (deviceEntity == null)
            {
                return NotFound();
            }

            var deviceGroupDeviceEntity = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                           where deviceGroupDevice.DeviceGroupReferenceId == request.DeviceGroupReferenceId &&
                                           deviceGroupDevice.DeviceReferenceId == request.DeviceReferenceId
                                           select deviceGroupDevice)
                                           ?.FirstOrDefault();
            _dbContext.DeviceGroupDevices.Remove(deviceGroupDeviceEntity);
            _logger.LogInformation(LogEvent.DeviceGroupApi, 
                $"已从设备组 {request.DeviceGroupReferenceId} " +
                $"移除设备 {request.DeviceReferenceId}");
            return Ok();
        }

        /// <summary>
        /// 更新设备组设备列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("update_devices")]
        public ActionResult<UpdateDeviceGroupDevicesResponse> UpdateDeviceGroupDevices([FromBody]UpdateDeviceGroupDevicesRequest request)
        {
            if (request?.DeviceReferenceIds == null ||
                string.IsNullOrEmpty(request.DeviceGroupReferenceId))
            {
                return BadRequest("请求为空");
            }

            var deviceGroupEntity = (from deviceGroup in _dbContext.DeviceGroups
                                     where deviceGroup.DeviceGroupReferenceId == request.DeviceGroupReferenceId
                                     select deviceGroup)
                                     .FirstOrDefault();
            if (deviceGroupEntity == null)
            {
                return NotFound($"找不到设备组{request.DeviceGroupReferenceId}");
            }

            var response = new UpdateDeviceGroupDevicesResponse();

            var updatingDeviceReferenceIds = new List<string>();
            request.DeviceReferenceIds.ForEach(d =>
            {
                var deviceEntity = (from device in _dbContext.Devices
                                    where device.DeviceReferenceId == d
                                    select device).FirstOrDefault();
                if (deviceEntity == null)
                {
                    response.SkippedDeviceReferenceIds.Add(d);
                    response.SkippedDeviceCount++;
                }
                else
                {
                    updatingDeviceReferenceIds.Add(d);
                }
            });
            
            var updatingDeviceGroupDeviceEntities = new List<DeviceGroupDevice>();
            updatingDeviceReferenceIds.ForEach(d =>
            {
                updatingDeviceGroupDeviceEntities.Add(new DeviceGroupDevice()
                {
                    DeviceGroupReferenceId = request.DeviceGroupReferenceId,
                    DeviceReferenceId = d
                });
            });

            var currentDeviceGroupDeviceEntities = (from deviceGroupDevice in _dbContext.DeviceGroupDevices
                                                    where deviceGroupDevice.DeviceGroupReferenceId == deviceGroupEntity.DeviceGroupReferenceId
                                                    select deviceGroupDevice)
                                                    .ToList();

            if (currentDeviceGroupDeviceEntities.Any())
            {
                _dbContext.DeviceGroupDevices.RemoveRange(currentDeviceGroupDeviceEntities);
            }
            _dbContext.DeviceGroupDevices.AddRange(updatingDeviceGroupDeviceEntities);
            _dbContext.SaveChanges();
            return Ok(JsonConvert.SerializeObject(response));
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

            var deviceGroupEntities = (from deviceGroup in _dbContext.DeviceGroups
                                       join deviceGroupUser in _dbContext.DeviceGroupUsers
                                       on deviceGroup.DeviceGroupReferenceId equals deviceGroupUser.DeviceGroupReferenceId
                                       where deviceGroupUser.UserReferenceId == userReferenceId
                                       select deviceGroup)
                                       ?.ToList();
            var response = new List<GetDeviceGroupsByUserResponse>();
            foreach(var deviceGroup in deviceGroupEntities)
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
                }) ;
            }
            return Ok(JsonConvert.SerializeObject(response));
        }
    

        /// <summary>
        /// 获取设备组设备及未分组设备
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("get_device")]
        public ActionResult<GetDeviceGroupDevicesResponse> GetDeviceGroupDevices([FromBody]GetDeviceGroupDevicesReqeust request)
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
    }
}
