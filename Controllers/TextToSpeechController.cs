using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Services.TextToSpeech;
using NetworkSoundBox.Controllers.DTO;
using Newtonsoft.Json;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.Message;

namespace NetworkSoundBox.Controllers
{
    [Route("api/")]
    [ApiController]
    public class TextToSpeechController : ControllerBase
    {
        private readonly IXunfeiTtsService _xunfeiTtsService;
        private readonly IDeviceContext _deviceService;

        public TextToSpeechController(IXunfeiTtsService xunfeiTtsService, IDeviceContext deviceSvrService)
        {
            _xunfeiTtsService = xunfeiTtsService;
            _deviceService = deviceSvrService;
        }

        [HttpGet("BluetoothPlayerTTS/TTS/{text}")]
        public async Task<FileResult> GetSpeech(string text)
        {
            var speech = await _xunfeiTtsService.GetSpeech(text);
            return new FileContentResult(speech.ToArray(), "audio/mpeg");
        }

        [HttpGet("TTS/{text}/vcn{vcn}/speed{speed}/volumn{volumn}/pitch{pitch}")]
        public async Task<FileResult> GetSpeech(string text, VCN vcn = VCN.XIAOYAN, int speed = 50, int volumn = 50, int pitch = 50)
        {
            var speech = await _xunfeiTtsService.GetSpeech(text, vcn.ToString().ToLower(), speed, volumn, pitch);
            return new FileContentResult(speech.ToArray(), "audio/mpeg");
        }

        [HttpGet("DownloadTTS/sn{sn}/text{text}/vcn{vcn}/speed{speed}/volumn{volumn}")]
        public async Task<string> DownloadTTS(string sn, string text, VCN vcn = VCN.XIAOYAN, int speed = 50, int volumn = 50)
        {
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
            var speech = await _xunfeiTtsService.GetSpeech(text, vcn.ToString().ToLower(), speed, volumn, 50);
            var fileUploaded = new File(speech);
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

    public enum VCN
    {
        XIAOYAN,
        AISJIUXU,
        AISXPING,
        AISJINGER,
        AISBABYXU
    }
}
