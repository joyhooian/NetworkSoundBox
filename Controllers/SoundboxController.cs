using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Models;
using Newtonsoft.Json;
using System.Net.Http;
using NetworkSoundBox.WxAuthorization.QRCode.DTO;
using NetworkSoundBox.WxAuthorization.QRCode;
using Microsoft.AspNetCore.Authorization;
using NetworkSoundBox.Authorization.WxAuthorization.Login;
using NetworkSoundBox.Authorization;
using NetworkSoundBox.Authorization.DTO;
using Microsoft.AspNetCore.SignalR;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Controllers.DTO;

namespace NetworkSoundBox.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SoundboxController : ControllerBase
    {
        private readonly IDeviceSvrService _deviceService;
        private readonly MySqlDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWxLoginQRService _wxLoginQRService;
        private readonly IWxLoginService _wxLoginService;
        private readonly IJwtAppService _jwtAppService;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public SoundboxController(
            IDeviceSvrService tcpService,
            MySqlDbContext dbContext,
            IHttpClientFactory httpClientFactory, 
            IWxLoginQRService wxLoginQRService,
            IWxLoginService wxLoginService,
            IJwtAppService jwtAppService,
            IHubContext<NotificationHub> notificationHub)
        {
            _deviceService = tcpService;
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _wxLoginQRService = wxLoginQRService;
            _wxLoginService = wxLoginService;
            _jwtAppService = jwtAppService;
            _notificationHub = notificationHub;
        }

        [HttpGet("wxlogin")]
        public async Task<string> WxLoginAsync()
        {
            WxLoginQRDto dto = await _wxLoginQRService.RequestLoginQRAsync();
            return JsonConvert.SerializeObject(dto);
        }

        [HttpGet("logintest{loginKey}")]
        public bool LoginTest(string loginKey)
        {
            var user = _dbContext.User.FirstOrDefault();
            var jwt = _jwtAppService.Create(new UserDto
            {
                Id = user.UserId,
                UserName = user.Name,
                Email = user.Email,
                Phone = user.TelNum,
                Role = user.Role
            });
            var client = NotificationHub.ClientHashSet.FirstOrDefault(c => c.LoginKey == loginKey);
            if (client != null)
            {
                client.LoginKey = jwt.Token;
                _notificationHub.Clients.Client(client.ClientId).SendAsync("LoginToken", jwt.Token);
                return true;
            }
            return false;
        }

        [HttpGet("wxapi/code2session/{code}/loginkey/{loginKey}")]
        public async Task<bool> Code2Session(string code, string loginKey)
        {
            string openId = await _wxLoginService.Code2Session(code);
            var user = _dbContext.User.FirstOrDefault(u => u.OpenId == openId);
            var jwt = _jwtAppService.Create(new UserDto
            {
                Id = user.UserId,
                UserName = user.Name,
                Email = user.Email,
                Phone = user.TelNum,
                Role = user.Role
            });
            var client = NotificationHub.ClientHashSet.FirstOrDefault(c => c.LoginKey == loginKey);
            if (client != null)
            {
                await _notificationHub.Clients.Client(client.ClientId).SendAsync(jwt.Token);
                return true;
            }
            return false;
        }

        [HttpGet("Devices/{id}")]
        public string GetDevices(int id)
        {
            using (_dbContext)
            {
                List<Device> list = _dbContext.Device.Where(device => device.userId == id).ToList();
                list.ForEach(device =>
                {
                    if (_deviceService.DevicePool.Find(d => d.SN == device.sn) != null)
                    {
                        device.isOnline = true;
                    }
                    device.activation = "";
                });
                return JsonConvert.SerializeObject(list);
            }
        }

        //[Authorize(Roles = "Admin")]
        [Authorize]
        [HttpGet("DevicesAdmin")]
        public string GetAllDevicesAdmin()
        {
            List<Device> list = _dbContext.Device.ToList();
            list.ForEach(device =>
            {
                if (_deviceService.DevicePool.Find(d => d.SN == device.sn) != null)
                    {
                    device.isOnline = true;
                }
            });
            return JsonConvert.SerializeObject(list);
        }

        [HttpGet("User/{id}")]
        public string GetUserName(int id)
        {
            using (_dbContext)
            {
                var user = _dbContext.User.Find(id);
                return user.Name;
            }
        }

        [HttpGet("PlayAndPause/SN{sn}Action{action}")]
        public string PlayAndPause(string sn, int action)
        {
            DeviceHandler device = null;
            try
            {
                device = _deviceService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            if (action == 1)
            {
                device.Socket.Send(new byte[] { 0x7E, 0x02, 0x01, 0xEF });
            }
            else if (action == 0)
            {
                device.Socket.Send(new byte[] { 0x7E, 0x02, 0x02, 0xEF });
            }
            return "Seccess!";
        }

        [HttpGet("NextAndPrevious/SN{sn}Action{action}")]
        public string NextAndPrevious(string sn, int action)
        {
            DeviceHandler device = null;
            try
            {
                device = _deviceService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            if (action == 1)
            {
                device.Socket.Send(new byte[] { 0x7E, 0x02, 0x03, 0xEF });
            }
            else if (action == 0)
            {
                device.Socket.Send(new byte[] { 0x7E, 0x02, 0x04, 0xEF });
            }
            return "Seccess!";
        }

        [HttpGet("Volumn/SN{sn}Action{action}")]
        public string Volumn(string sn, int action)
        {
            DeviceHandler device = null;
            try
            {
                device = _deviceService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            if (action == 1)
            {
                device.Socket.Send(new byte[] { 0x7E, 0x02, 0x05, 0xEF });
            }
            else if (action == 0)
            {
                device.Socket.Send(new byte[] { 0x7E, 0x02, 0x06, 0xEF });
            }
            return "Seccess!";
        }

        [HttpGet("StopPlay/SN{sn}")]
        public string StopPlay(string sn)
        {
            DeviceHandler device = null;
            try
            {
                device = _deviceService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            device.Socket.Send(new byte[] { 0x7E, 0x02, 0x0E, 0xEF });
            return "Seccess!";
        }

        [HttpGet("TTS/SN{sn}Text{text}")]
        public FileResult TTS(string sn, string text)
        {
            Console.WriteLine("New TTS Task is required to device[{0}] with {1} words", sn, text.Length);
            byte[] receiveBuffer = new byte[1024 * 1024 * 10];
            int contentLength = 0;

            var request = new HttpRequestMessage(HttpMethod.Get, "https://tts.jhy2015.cn/api/BluetoothPlayerTTS/TTS/" + text);
            var client = _httpClientFactory.CreateClient();
            var response = client.Send(request);

            if(response.IsSuccessStatusCode)
            {
                var responseStream = response.Content.ReadAsStream();
                contentLength = responseStream.Read(receiveBuffer);
                ArraySegment<byte> content = new ArraySegment<byte>(receiveBuffer, 0, contentLength);

                DeviceHandler device = null;
                try
                {
                    device = _deviceService.DevicePool.First(device => device.SN == sn);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine(ex.Message);
                }
                if (device == null)
                {
                    return null;
                    //return "Filed! Device is not connected!";
                }
                device.FileQueue.Add(new(content.ToList()));
                return new FileContentResult(content.ToArray(), "audio/mp3");
            }
            return null;
            //return "Success!";
        }

        [HttpPost("TransFileList/SN{sn}")]
        public bool TransFile(string sn, List<IFormFile> files)
        {
            Console.WriteLine("Upload {0} files", files.Count);

            DeviceHandler device = null;
            try
            {
                device = _deviceService.DevicePool.First(device => device.SN == sn);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (device == null)
            {
                return false;
            }

            int index = 0;
            files.ForEach(file =>
            {
                index++;
                Console.WriteLine("File{0} has {1} Kbyte", index, file.Length / 1024);
                byte[] content = new byte[1024 * 1024 * 10];
                int contentLength = file.OpenReadStream().Read(content);
                device.FileQueue.Add(new(new ArraySegment<byte>(content, 0, contentLength).ToList()));
            });
            return true;
        }

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
                device = _deviceService.DevicePool.First(device => device.SN == sn);
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
