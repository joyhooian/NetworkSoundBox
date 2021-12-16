using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.Device.Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using NetworkSoundBox.Filter;
using System.Net.Http;
using NetworkSoundBox.Controllers.DTO;
using Microsoft.AspNetCore.Http;
using NetworkSoundBox.Services.Message;
using System.Text;
using System.Threading;
using NetworkSoundBox.Services.TextToSpeech;
using System.Threading.Tasks;

namespace NetworkSoundBox.Controllers
{
    [Route("api/device_ctrl")]
    [ApiController]
    [ServiceFilter(typeof(ResourceAuthAttribute))]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceContext _deviceContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IXunfeiTtsService _xunfeiTtsService;

        public DeviceController(
            IXunfeiTtsService xunfeiTtsService,
            IHttpClientFactory httpClientFactory, 
            IDeviceContext deviceContext)
        {
            _httpClientFactory = httpClientFactory;
            _deviceContext = deviceContext;
            _xunfeiTtsService = xunfeiTtsService;
        }

        /// <summary>
        /// 获取播放列表
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("play_list")]
        public IActionResult GetPlayList([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            int result = device.GetPlayList();
            return result != -1 ? Ok(result) : BadRequest("设备未响应");
        }

        /// <summary>
        /// 删除指定音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("delete_audio")]
        public IActionResult DeleteAudio([FromQuery] string sn, int index)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.DeleteAudio(index) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("play_index")]
        public IActionResult PlayIndex([FromQuery] string sn, int index)
        {
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
        [HttpPost("play_pause")]
        public IActionResult PlayOrPause([FromQuery] string sn, int action)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendPlayOrPause(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 上一首或下一首
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 下一首; 2: 上一首</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("next_previous")]
        public IActionResult NextOrPrevious([FromQuery] string sn, int action)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendNextOrPrevious(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备音量
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="volumn">音量(0~30)</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("volumn")]
        public IActionResult Volumn([FromQuery] string sn, int volumn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendVolumn(volumn) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备重启
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("reboot")]
        public IActionResult Reboot([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendReboot() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备重置
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("restore")]
        public IActionResult Restore([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendRestore() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 下发定时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("cron_task")]
        public IActionResult SetAlarms([FromQuery] string sn, [FromBody] TimeSettingDto dto)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];

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

            return device.SendCronTask(data) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 下发延时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("delay_task")]
        public IActionResult SetAlarmsAfter([FromQuery] string sn, [FromBody] TimeSettingAfterDto dto)
        {
            var device = _deviceContext.DevicePool[sn];

            List<byte> data = new();
            data.Add((byte)((dto.TimeDelay & 0xFF00) >> 8));
            data.Add((byte)(dto.TimeDelay & 0x00FF));
            data.Add((byte)dto.Volumn);
            data.Add((byte)(dto.Relay ? 0x01 : 0x00));
            data.Add((byte)dto.Audio);

            return device.SendDelayTask(data) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("file/sn{sn}")]
        public IActionResult TransFile(string sn, IFormFile formFile)
        {
            var device = _deviceContext.DevicePool[sn];
            byte[] content = new byte[formFile.Length];
            formFile.OpenReadStream().Read(content);
            if (device.Type == Services.Message.DeviceType.WiFi_Test)
            {
                var fileUploadHandle = new File(content.ToList());
                device.FileQueue.Add(fileUploadHandle);
                fileUploadHandle.Semaphore.WaitOne();
                return fileUploadHandle.FileStatus == FileStatus.Success ? Ok() : BadRequest("文件传输失败");
            }
            else
            {
                FileContentResult fileContentResult = new(content, "audio/mp3");
                string fileToken = Guid.NewGuid().ToString("N")[..8];
                var fileUploadHandle = new KeyValuePair<Semaphore, FileContentResult>(new Semaphore(0, 1), fileContentResult);
                _deviceContext.FileList.Add(fileToken, fileUploadHandle);

                if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");

                var ret = fileUploadHandle.Key.WaitOne(1000 * 60);
                _deviceContext.FileList.Remove(fileToken);
                return ret ? Ok() : BadRequest("传输超时");
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

        [Authorize]
        [HttpPost("upload_tts")]
        public async Task<IActionResult> UploadTts([FromQuery] string sn, string text, VCN vcn = VCN.XIAOYAN, int speed = 50, int volumn = 50)
        {
            var device = _deviceContext.DevicePool[sn];
            var speech = await _xunfeiTtsService.GetSpeech(text, vcn.ToString().ToLower(), speed, volumn, 50);
            byte[] audioContent = new byte[speech.Count];
            speech.CopyTo(audioContent, 0);

            FileContentResult fileContentResult = new(audioContent, "audio/mpeg");
            string fileToken = Guid.NewGuid().ToString("N")[..8];
            var fileUploadHandle = new KeyValuePair<Semaphore, FileContentResult>(new Semaphore(0, 1), fileContentResult);
            _deviceContext.FileList.Add(fileToken, fileUploadHandle);

            if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");
            var ret = fileUploadHandle.Key.WaitOne(1000 * 60);
            _deviceContext.FileList.Remove(fileToken);
            return ret ? Ok() : BadRequest("传输超时");
        }

        [HttpGet("download_file_test")]
        public async Task<IActionResult> DownloadFileTest([FromQuery] string fileToken)
        {
            if (fileToken == "12345678")
            {
                if (_deviceContext.FileContentResult_Test == null)
                {
                    var text = "测试音频,测试音频,测试音频,测试音频,测试音频,测试音频,测试音频,测试音频";
                    var speech = await _xunfeiTtsService.GetSpeech(text, "xiaoyan", 50, 50, 50);
                    byte[] audioContent = new byte[speech.Count];
                    speech.CopyTo(audioContent, 0);
                    _deviceContext.FileContentResult_Test = new FileContentResult(audioContent, "audio/mpeg");
                    return _deviceContext.FileContentResult_Test;
                }
                return _deviceContext.FileContentResult_Test;
            }
            return BadRequest("参数不正确");
        }
    }
}
