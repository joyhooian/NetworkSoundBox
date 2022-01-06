using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
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

        [HttpGet("logintest{loginKey}role{role}")]
        public IActionResult LoginTest(string loginKey, string role)
        {
            if (!Enum.TryParse(typeof(RoleType), role, true, out var roleType)) return BadRequest("未知参数");
            var userEntity = _dbContext.Users.FirstOrDefault(u => u.Role == (int)roleType);
            if (userEntity == null) return BadRequest("未知参数");
            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel);
            if (_notificationContext.ClientDict.ContainsKey(loginKey))
            {
                _notificationContext.SendClientLogin(loginKey, jwt.Token);
                return Ok();
            }
            return BadRequest();
        }

        [Authorize(Roles = "admin")]
        [HttpGet("overall")]
        public string GetOverallData()
        {
            var userCount = _dbContext.Users.Count();
            var deviceCount = _dbContext.Devices.Count();
            var activedCount = _dbContext.Devices.Count(device => device.IsActived == 1);
            var onlineCount = _deviceContext.DevicePool.Count;

            return JsonConvert.SerializeObject(new GetDeviceOverallResponse
            {
                UserCount = userCount,
                DeviceCount = deviceCount,
                ActivedCount = activedCount,
                OnlineCount = onlineCount
            });
        }

        [Authorize(Roles = "admin")]
        [HttpPost("add_device")]
        public string AddDevice([FromBody] DeviceAdminDto dto)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == dto.Sn);
            if (deviceEntity != null)
            {
                return "Fail. The device has already existed.";
            }
            dto.ActivationKey = Guid.NewGuid().ToString();
            deviceEntity = _mapper.Map<DeviceAdminDto, Device>(dto);
            deviceEntity.Id = 1;
            _dbContext.Devices.Add(deviceEntity);
            _dbContext.SaveChanges();
            return "Success";
        }

        [Authorize(Roles = "admin")]
        [HttpPost("del_device")]
        public string DeleteDevice([FromQuery] string sn)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            if (deviceEntity == null)
            {
                return "Fail";
            }
            _dbContext.Devices.Remove(deviceEntity);
            _dbContext.SaveChanges();
            return "Success";
        }

        [Authorize(Roles = "admin")]
        [HttpPost("edit_device")]
        public IActionResult EditDeviceAdmin([FromBody] EditDeviceRequest request)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == request.Sn);
            if (deviceEntity == null)
            {
                return BadRequest("Fail. Device is not existed.");
            }
            try
            {
                var tempDeviceEntity = _mapper.Map<EditDeviceRequest, Device>(request);
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

        [Authorize(Roles = "admin")]
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

        [Authorize(Roles = "admin")]
        [HttpPost("manual_deactive")]
        public IActionResult ManualDeactive([FromQuery] string sn)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            if (deviceEntity == null)
            {
                return BadRequest("Fail. Device is not existed.");
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
        [HttpGet("Devices")]
        public IActionResult GetDevicesByUser()
        {
            var openId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(openId)) return BadRequest("没有权限");

            try
            {
                var userEntity = _dbContext.Users.FirstOrDefault(x => x.OpenId == openId);
                var devices = (from device in _dbContext.Devices
                               join userDevice in _dbContext.UserDevices
                               on device.DeviceReferenceId equals userDevice.DeviceRefrenceId
                               where userDevice.UserRefrenceId == userEntity.UserRefrenceId
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
                });
                return Ok(JsonConvert.SerializeObject(responses));
            }
            catch(Exception ex)
            {
                _logger.LogError(LogEvent.DeviceMaintainApi, ex, "While GetDevicesByUser is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost("is_online")]
        public IActionResult GetDeviceOnlineStatus([FromQuery] string sn)
        {
            var openId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(openId)) return BadRequest("没有权限");
            if (_deviceContext.DevicePool.TryGetValue(sn, out DeviceHandler device))
            {
                if (device.UserOpenId != openId) return BadRequest("没有权限");
                return Ok(true);
            }
            return Ok(false);
        }

        [Authorize(Roles = "admin")]
        [HttpPost("DevicesAdmin")]
        public string GetAllDevicesAdmin()
        {
            var list = _dbContext.Devices
                .Select(device => _mapper.Map<Device, DeviceAdminDto>(device))
                .ToList();
            list.ForEach(device =>
            {
                if (_deviceContext.DevicePool.ContainsKey(device.Sn))
                {
                    device.IsOnline = true;
                }
            });
            return JsonConvert.SerializeObject(list);
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

        #region 播放控制
        [Obsolete]
        /// <summary>
        /// 获取播放列表
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [HttpPost("play_list")]
        public string GetPlayList([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null) return "Failed! Device is not connected!";

            int ret = device.GetPlayList();
            if (ret == -1) return "Failed!";

            return ret.ToString();
        }

        [Obsolete]
        /// <summary>
        /// 删除指定音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [HttpPost("delete_audio")]
        public string DeleteAudio([FromQuery] string sn, int index)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null) return "Failed! Device is not connected!";

            if (device.DeleteAudio(index)) return "Success!";
            return "Failed!";
        }
        
        [Obsolete]
        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [HttpPost("play_index")]
        public string PlayIndex([FromQuery] string sn, int index)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null) return "Failed! Device is not connected!";

            if (device.PlayIndex(index)) return "Success!";
            return "Failed!";
        }

        [Obsolete]
        /// <summary>
        /// 播放或暂停
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 播放; 2: 暂停</param>
        /// <returns></returns>
        [HttpPost("play_pause")]
        public string PlayOrPause([FromQuery] string sn, int action)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null)
                return "Failed! Device is not connected!";

            if (action != 0 && action != 1)
                return "Failed! Invalid params";

            return device.SendPlayOrPause(action) ? "Success" : "Failed";
        }

        [Obsolete]
        /// <summary>
        /// 上一首或下一首
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 下一首; 2: 上一首</param>
        /// <returns></returns>
        [HttpPost("next_previous")]
        public string NextOrPrevious([FromQuery] string sn, int action)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null)
                return "Failed! Device is not connected!";

            if (action != 0 && action != 1)
                return "Failed! Invalid param.";

            return device.SendNextOrPrevious(action) ? "Success" : "Failed";
        }

        [Obsolete]
        /// <summary>
        /// 设备音量
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="volumn">音量(0~30)</param>
        /// <returns></returns>
        [HttpPost("volumn")]
        public string Volumn([FromQuery] string sn, int volumn)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;
            if (device == null)
                return "Failed! Device is not connected!";

            if (!(0 <= volumn && volumn <= 30))
                return "Failed! Invalid param.";

            return device.SendVolume(volumn) ? "Success!" : "Failed";
        }
        #endregion

        #region 设备控制
        [Obsolete]
        /// <summary>
        /// 设备重启
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [HttpPost("reboot")]
        public string Reboot(string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null)
            {
                return "Failed! Device is not connected.";
            }

            return device.SendReboot() ? "Success!" : "Failed";
        }

        [Obsolete]
        [HttpPost("restore")]
        public string Restore(string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;

            if (device == null)
            {
                return "Failed! Device is not connected!";
            }

            return device.SendRestore() ? "Success!" : "Failed!";
        }
        #endregion

        [Obsolete]
        [HttpPost("alarms")]
        public string SetAlarms(CronTaskDto dto)
        {
            if (dto == null)
            {
                return "Fail";
            }
            DeviceHandler device = null;
            try
            {
                device = _deviceContext.DevicePool.First(pair => pair.Key == dto.Sn).Value;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (device == null)
            {
                return "Fail. Device hasn't connected";
            }
            List<byte> data = new();
            data.Add((byte)dto.Index);
            data.Add((byte)dto.StartTime.Hour);
            data.Add((byte)dto.StartTime.Minute);
            data.Add((byte)dto.EndTime.Hour);
            data.Add((byte)dto.EndTime.Minute);
            data.Add((byte)dto.Volume);
            data.Add((byte)(dto.Relay ? 0x01 : 0x00));
            dto.Weekdays.ForEach(d =>
            {
                data.Add((byte)(d + 1));
            });
            data.Add((byte)dto.Audio);
            if (device.SendCronTask(data))
            {
                return "Success";
            }
            return "Fail";
        }

        [Obsolete]
        [HttpPost("TransFile/SN{sn}")]
        public string TransFile(string sn, IFormFile formFile)
        {
            if (formFile == null)
            {
                return JsonConvert.SerializeObject(new FileResultDto("fail", "Empty of file content."));
            }

            Console.WriteLine("Upload 1 file");

            DeviceHandler device = null;
            try
            {
                device = _deviceContext.DevicePool.First(pair => pair.Key == sn).Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (device == null)
            {
                return JsonConvert.SerializeObject(new FileResultDto("fail", "Device is disconnected."));
            }

            Console.WriteLine("File has {0} Kbyte", formFile.Length / 1024);
            byte[] content = new byte[1024 * 1024 * 10];
            int contentLength = formFile.OpenReadStream().Read(content);
            var fileUploaded = new File(new ArraySegment<byte>(content, 0, contentLength).ToList());
            device.FileQueue.Add(fileUploaded);
            fileUploaded.Semaphore.WaitOne();
            if (fileUploaded.FileStatus == FileStatus.Success)
            {
                return JsonConvert.SerializeObject(new FileResultDto("success", ""));
            }
            else
            {
                return JsonConvert.SerializeObject(new FileResultDto("fail", "Transmit file failed."));
            }
        }
    }
}
