using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Authorization.Jwt;
using NetworkSoundBox.Authorization.Secret.DTO;
using NetworkSoundBox.Authorization.WxAuthorization.Login;
using NetworkSoundBox.Authorization.WxAuthorization.QRCode;
using NetworkSoundBox.Authorization.WxAuthorization.QRCode.DTO;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.Message;
using Newtonsoft.Json;

namespace NetworkSoundBox.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SoundboxController : ControllerBase
    {
        private readonly IDeviceContext _deviceContext;
        private readonly MySqlDbContext _dbContext;
        private readonly IWxLoginQRService _wxLoginQRService;
        private readonly IWxLoginService _wxLoginService;
        private readonly IJwtAppService _jwtAppService;
        private readonly INotificationContext _notificationContext;
        private readonly IMapper _mapper;

        public SoundboxController(
            IDeviceContext tcpService,
            MySqlDbContext dbContext,
            IWxLoginQRService wxLoginQRService,
            IWxLoginService wxLoginService,
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
        }

        [HttpGet("wxlogin")]
        public async Task<string> WxLoginAsync()
        {
            WxLoginQRDto dto = await _wxLoginQRService.RequestLoginQRAsync();
            return JsonConvert.SerializeObject(dto);
        }

        [HttpGet("logintest{loginKey}role{role}")]
        public IActionResult LoginTest(string loginKey, string role)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Role == role);
            var jwt = _jwtAppService.Create(new UserDto
            {
                Id = (int)user.Id,
                OpenId = user.Openid,
                Role = user.Role
            });
            if (_notificationContext.ClientDict.ContainsKey(loginKey))
            {
                _notificationContext.SendClientLogin(loginKey, jwt.Token);
                return Ok();
            }
            return BadRequest();
        }

        [HttpGet("wxapi/code2session/{code}/loginkey/{loginKey}")]
        public async Task<IActionResult> Code2Session(string code, string loginKey)
        {
            string openId = await _wxLoginService.Code2Session(code);
            var user = _dbContext.Users.FirstOrDefault(u => u.Openid == openId);
            var jwt = _jwtAppService.Create(new UserDto
            {
                Id = (int)user.Id,
                OpenId = user.Openid,
                Role = user.Role
            });
            await _notificationContext.SendClientLogin(loginKey, jwt.Token);
            return Ok();
        }

        [HttpPost("wx/login")]
        public async Task<string> WxLogin(string code)
        {
            string openId = await _wxLoginService.Code2Session(code);
            var user = _dbContext.Users.FirstOrDefault(u => u.Openid == openId);
            if (user == null)
            {
                user = new User
                {
                    Name = Guid.NewGuid().ToString("N"),
                    Openid = openId,
                    Role = "customer"
                };
                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();
            }
            _dbContext.Entry(user);

            var jwt = _jwtAppService.Create(_mapper.Map<User, UserDto>(user));

            return JsonConvert.SerializeObject(new LoginResultDto
            {
                UserInfo = _mapper.Map<User, UserInfoDto>(user),
                Status = "success",
                Token = jwt.Token,
                ErrorMessage = ""
            });
        }

        [Authorize(Roles = "admin")]
        [HttpGet("overall")]
        public string GetOverallData()
        {
            var userCount = _dbContext.Users.Count();
            var deviceCount = _dbContext.Devices.Count();
            var activedCount = _dbContext.Devices.Count(device => device.Activation == 1);
            var onlineCount = _deviceContext.DevicePool.Count;

            return JsonConvert.SerializeObject(new OverallDto
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
            deviceEntity.UserId = 1;
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
        public string EditDevice([FromBody] DeviceAdminDto dto)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == dto.Sn);
            if (deviceEntity == null)
            {
                return "Fail. Device is not existed.";
            }
            try
            {
                deviceEntity.UserId = (uint)dto.UserId;
                deviceEntity.Name = dto.Name;
                deviceEntity.DeviceType = dto.DeviceType;
                _dbContext.SaveChanges();
            }
            catch (Exception)
            {
                return "Fail. Invalid param";
            }
            return "Success";
        }

        [Authorize(Roles = "admin")]
        [HttpPost("manual_active")]
        public string ManualActive([FromQuery] string sn)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            if (deviceEntity == null)
            {
                return "Fail. Device is not existed.";
            }
            if (deviceEntity.Activation == 1)
            {
                return "Warn. Device has already been actived.";
            }
            deviceEntity.Activation = 1;
            _dbContext.SaveChanges();
            return "Success.";
        }

        [Authorize(Roles = "admin")]
        [HttpPost("manual_deactive")]
        public string ManualDeactive([FromQuery] string sn)
        {
            Device deviceEntity = _dbContext.Devices.FirstOrDefault(x => x.Sn == sn);
            if (deviceEntity == null)
            {
                return "Fail. Device is not existed.";
            }
            if (deviceEntity.Activation == 0)
            {
                return "Warn. Device has already been deactived.";
            }
            deviceEntity.Activation = 0;
            _dbContext.SaveChanges();
            return "Success";
        }

        [Authorize]
        [HttpGet("Devices")]
        public async Task<string> GetDevicesByUser()
        {
            var token = await HttpContext.GetTokenAsync("Bearer", "access_token");
            var id = _jwtAppService.GetUserId(token);
            List<DeviceCustomerDto> list = _dbContext.Devices
                .Where(device => device.UserId == id)
                .Select(device => _mapper.Map<Device, DeviceCustomerDto>(device))
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

        [Authorize(Roles = "admin")]
        [HttpGet("DevicesAdmin")]
        public string GetAllDevicesAdmin()
        {
            
            List<DeviceAdminDto> list = _dbContext.Devices
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

            return device.SendVolumn(volumn) ? "Success!" : "Failed";
        }
        #endregion

        #region 设备控制
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

        [HttpPost("alarms")]
        public string SetAlarms(TimeSettingDto dto)
        {
            if (dto == null)
            {
                return "Fail";
            }
            DeviceHandler device = null;
            try
            {
                device = DeviceContext._devicePool.First(pair => pair.Key == dto.Sn).Value;
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
            data.Add((byte)dto.Volumn);
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

        [HttpPost("alarms_timeAfter")]
        public string SetAlarmsAfter([FromBody] TimeSettingAfterDto dto)
        {
            if (dto == null)
            {
                return "Fail";
            }
            DeviceHandler device = null;
            try
            {
                device = DeviceContext._devicePool.First(pair => pair.Key == dto.Sn).Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (device == null)
            {
                return "Fail. Device hasn't connected";
            }
            List<byte> data = new();
            data.Add((byte)((dto.TimeDelay & 0xFF00) >> 8));
            data.Add((byte)(dto.TimeDelay & 0x00FF));
            data.Add((byte)dto.Volumn);
            data.Add((byte)(dto.Relay ? 0x01: 0x00));
            data.Add((byte)dto.Audio);
            if (device.SendDelayTask(data))
            {
                return "Success";
            }
            else
            {
                return "Fail";
            }
        }

        //[HttpPost("transfile_cellular")]
        //public IActionResult TransFileCellular(string sn, IFormFile file)
        //{
        //    if (file == null)
        //    {
        //        throw new HttpRequestException("文件为空");
        //    }

        //    // 文件大小应小于50M
        //    if (file.Length >= 1024 * 1024 * 50)
        //    {
        //        throw new HttpRequestException("文件过大");
        //    }

        //    // 文件类型应当为MP3
        //    //if (file.ContentType != "audio/mpeg")
        //    //{
        //    //    throw new HttpRequestException("文件格式错误");
        //    //}

        //    Console.WriteLine("Upload 1 file");

        //    DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(pair => pair.Key == sn).Value;
        //    //if (device == null)
        //    //{
        //    //    throw new HttpRequestException("设备未连接");
        //    //}

        //    DateTimeOffset dateTimeOffset = DateTimeOffset.Now;
        //    string fileToken = Guid.NewGuid().ToString("N")[..8];
        //    byte[] data = new byte[file.Length];
        //    file.OpenReadStream().Read(data);
        //    FileContentResult fileContentResult = new(data, "audio/mp3");
        //    _deviceContext.FileList.Add(fileToken, new(dateTimeOffset, fileContentResult));
        //    var fileTokenBytes = Encoding.ASCII.GetBytes(fileToken);
        //    if (device.ReqFileTrans(fileTokenBytes))
        //    {
        //        return Ok(fileToken);
        //    }
        //    else
        //    {
        //        throw new HttpRequestException("设备未响应");
        //    }
        //}

        //[HttpGet("downloadfile_cellular")]
        //public IActionResult DownloadFileCellular([FromQuery] string fileToken)
        //{
        //    if (_deviceContext.FileList.TryGetValue(fileToken, out KeyValuePair<DateTimeOffset, FileContentResult> filePair))
        //    {
        //        _deviceContext.FileList.Remove(fileToken);
        //        return filePair.Value;
        //    }
        //    else
        //    {
        //        return BadRequest();
        //    }
        //}

        [HttpPost("TransFile/SN{sn}")]
        public string TransFile(string sn, IFormFile file)
        {
            if (file == null)
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

            Console.WriteLine("File has {0} Kbyte", file.Length / 1024);
            byte[] content = new byte[1024 * 1024 * 10];
            int contentLength = file.OpenReadStream().Read(content);
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
