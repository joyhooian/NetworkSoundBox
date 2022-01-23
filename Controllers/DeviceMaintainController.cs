using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Controllers.Model;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Authorization.Jwt;
using NetworkSoundBox.Middleware.Authorization.Wechat.Login;
using NetworkSoundBox.Middleware.Authorization.Wechat.QRCode;
using NetworkSoundBox.Middleware.Authorization.Wechat.QRCode.Model;
using NetworkSoundBox.Middleware.Hubs;
using NetworkSoundBox.Middleware.Logger;
using NetworkSoundBox.Models;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.Message;
using Newtonsoft.Json;
using Nsb.Type;

namespace NetworkSoundBox.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceMaintainController : ControllerBase
    {
        private readonly ILogger<DeviceMaintainController> _logger;
        private readonly IDeviceContext _deviceContext;
        private readonly MySqlDbContext _dbContext;
        private readonly IWechatQrService _wxLoginQRService;
        private readonly IWechatLoginService _wxLoginService;
        private readonly IJwtAppService _jwtAppService;
        private readonly INotificationContext _notificationContext;
        private readonly IMapper _mapper;

        public DeviceMaintainController(
            ILogger<DeviceMaintainController> logger,
            IDeviceContext tcpService,
            MySqlDbContext dbContext,
            IWechatQrService wxLoginQRService,
            IWechatLoginService wxLoginService,
            IJwtAppService jwtAppService,
            INotificationContext notificationContext,
            IMapper mapper)
        {
            _deviceContext = tcpService;
            _dbContext = dbContext;
            _wxLoginQRService = wxLoginQRService;
            _wxLoginService = wxLoginService;
            _jwtAppService = jwtAppService;
            _notificationContext = notificationContext;
            _mapper = mapper;
            _logger = logger;
        }

        [HttpGet("wxlogin")]
        public async Task<string> WxLoginAsync()
        {
            WechatQrLoginData dto = await _wxLoginQRService.GetWechatLoginQrAsync();
            return JsonConvert.SerializeObject(dto);
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("overall_admin")]
        public string GetOverallDataAdmin()
        {
            var userCount = _dbContext.Users.Count();
            var deviceCount = _dbContext.Devices.Count();
            var activedCount = _dbContext.Devices.Count(device => device.IsActived == 1);
            var onlineCount = _deviceContext.DevicePool.Count;

            return JsonConvert.SerializeObject(new GetOverallAdminResponse
            {
                UserCount = userCount,
                DeviceCount = deviceCount,
                ActivedCount = activedCount,
                OnlineCount = onlineCount
            });
        }

        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("overall_customer")]
        public IActionResult GetOverrallDataCustomer()
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userRefrenceId)) return BadRequest("未授权");
            var deviceList = (from device in _dbContext.Devices
                              join userDevice in _dbContext.UserDevices
                              on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                              where userDevice.UserRefrenceId == userRefrenceId
                              select device).ToList();
            var deviceCount = deviceList.Count;
            var onlineCount = 0;
            deviceList.ForEach(device =>
            {
                if (_deviceContext.DevicePool.ContainsKey(device.Sn))
                {
                    onlineCount++;
                }
            });
            return Ok(JsonConvert.SerializeObject(new GetOverallCustomerResponse()
            {
                DeviceCount = deviceCount,
                OnlineCount = onlineCount
            }));
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_device")]
        public string AddDevice([FromBody] AddDeviceRequest request)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == request.Sn);
            if (deviceEntity != null)
            {
                return "Fail. The device has already existed.";
            }
            try
            {
                deviceEntity = _mapper.Map<AddDeviceRequest, Device>(request);
            }
            catch (Exception)
            {
                return "Failed";
            }
            deviceEntity.DeviceReferenceId = Guid.NewGuid().ToString();
            deviceEntity.ActivationKey = Guid.NewGuid().ToString();
            _dbContext.Devices.Add(deviceEntity);
            _dbContext.SaveChanges();
            return "Success";
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("del_device")]
        public string DeleteDevice([FromQuery] string sn)
        {
            var deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            var userDevice = (from ud in _dbContext.UserDevices
                              where ud.DeviceRefrenceId == deviceEntity.DeviceReferenceId
                              select ud).FirstOrDefault();
            if (userDevice != null)
            {
                _dbContext.UserDevices.Remove(userDevice);
            }
            if (deviceEntity == null)
            {
                return "Fail";
            }
            _dbContext.Devices.Remove(deviceEntity);
            _dbContext.SaveChanges();
            return "Success";
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("edit_device")]
        public IActionResult EditDeviceAdmin([FromBody] EditDeviceAdminRequest request)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == request.Sn);
            if (deviceEntity == null)
            {
                return BadRequest("Fail. Device is not existed.");
            }
            try
            {
                var tempDeviceEntity = _mapper.Map<EditDeviceAdminRequest, Device>(request);
                if (tempDeviceEntity.Type != 0) deviceEntity.Type = tempDeviceEntity.Type;
                deviceEntity.Name = tempDeviceEntity.Name;
                _dbContext.SaveChanges();
                _dbContext.Devices.Update(deviceEntity);
            }
            catch (Exception)
            {
                return BadRequest("Fail. Invalid param");
            }
            return Ok();
        }

        [Authorize(Roles = "customer")]
        [Authorize(Policy = "Permission")]
        [HttpPost("edit_device_customer")]
        public IActionResult EditDeviceCustomer([FromBody] EditDeviceCustomerRequest request)
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var userDeviceEntity = (from userDevice in _dbContext.UserDevices
                                    join device in _dbContext.Devices
                                    on userDevice.DeviceRefrenceId equals device.DeviceReferenceId
                                    where device.Sn == request.Sn
                                    select userDevice).FirstOrDefault();
            if (userDeviceEntity == null || userDeviceEntity.Permission > (int)PermissionType.Admin)
            {
                return BadRequest("无权操作");
            }
            var deviceEntity = (from device in _dbContext.Devices
                                where device.Sn == request.Sn
                                select device).FirstOrDefault();
            deviceEntity.Name = request.Name;
            _dbContext.SaveChanges();
            return Ok();
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("manual_active")]
        public IActionResult ManualActive([FromQuery] string sn)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            if (deviceEntity == null)
            {
                return BadRequest("Fail. Device is not existed.");
            }
            if (deviceEntity.IsActived == 1)
            {
                return Ok();
            }
            deviceEntity.IsActived = 1;
            _dbContext.SaveChanges();
            _dbContext.Devices.Update(deviceEntity);
            return Ok();
        }

        [Authorize]
        [HttpPost("active")]
        public IActionResult Active([FromQuery] string sn, string activeKey)
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            Device deviceEntity;
            deviceEntity = (from device in _dbContext.Devices
                            where device.ActivationKey == activeKey
                            select device).FirstOrDefault();
            if (deviceEntity == null)
            {
                return BadRequest("无此设备");
            }
            if (deviceEntity.IsActived == 1)
            {
                return Ok();
            }
            if (deviceEntity.ActivationKey == activeKey)
            {
                deviceEntity.IsActived = 1;
                _dbContext.UserDevices.Add(new UserDevice()
                {
                    UserRefrenceId = userRefrenceId,
                    DeviceRefrenceId = deviceEntity.DeviceReferenceId,
                    Permission = (int)Nsb.Type.PermissionType.Admin
                });
                _dbContext.SaveChanges();
                return Ok();
            }
            return BadRequest("未知错误");
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("manual_deactive")]
        public IActionResult ManualDeactive([FromQuery] string sn)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            if (deviceEntity == null)
            {
                return BadRequest("设备不存在");
            }
            if (deviceEntity.IsActived == 0)
            {
                return Ok();
            }
            deviceEntity.IsActived = 0;
            _dbContext.SaveChanges();
            _dbContext.Devices.Update(deviceEntity);
            return Ok();
        }

        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("user_devices")]
        public IActionResult GetDevicesByUser()
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userRefrenceId)) return BadRequest("没有权限");

            try
            {
                var devices = (from device in _dbContext.Devices
                               join userDevice in _dbContext.UserDevices
                               on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                               where userDevice.UserRefrenceId == userRefrenceId
                               select device)
                                   .ToList();
                var responses = new List<GetDevicesCustomerResponse>();
                devices.ForEach(device =>
                {
                    var response = _mapper.Map<Device, GetDevicesCustomerResponse>(device);
                    if (_deviceContext.DevicePool.ContainsKey(response.Sn))
                    {
                        response.IsOnline = true;
                    }
                    responses.Add(response);
                });
                return Ok(JsonConvert.SerializeObject(responses));
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.DeviceMaintainApi, ex, "While GetDevicesByUser is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("is_online")]
        public IActionResult GetDeviceOnlineStatus([FromQuery] string sn)
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var userDeviceEntity = (from device in _dbContext.Devices
                                    join userDevice in _dbContext.UserDevices
                                    on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                    where device.Sn == sn
                                    select userDevice).FirstOrDefault();
            if (userDeviceEntity == null) return BadRequest("没有权限");

            return Ok(_deviceContext.DevicePool.ContainsKey(sn));
        }

        [Authorize(Roles = "admin")]
        [Authorize(Policy = "Permission")]
        [HttpPost("DevicesAdmin")]
        public string GetAllDevicesAdmin()
        {
            var devices = _dbContext.Devices.Take(10).ToList();
            var deviceList = new List<GetDevicesAdminResponse>();
            foreach (var device in devices)
            {
                var response = _mapper.Map<Device, GetDevicesAdminResponse>(device);
                if (_deviceContext.DevicePool.ContainsKey(device.Sn))
                {
                    response.IsOnline = true;
                }
                deviceList.Add(response);
            }
            return JsonConvert.SerializeObject(deviceList);
        }

        [HttpGet("User/{id}")]
        public string GetUserName(int id)
        {
            using (_dbContext)
            {
                var user = _dbContext.Users.Find(id);
                return user.Name;
            }
        }
    }
}
