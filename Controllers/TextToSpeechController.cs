using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Services.TextToSpeech;

namespace NetworkSoundBox.Controllers
{
    [Route("api/")]
    [ApiController]
    public class TextToSpeechController : ControllerBase
    {
        private readonly IXunfeiTtsService _xunfeiTtsService;

        public TextToSpeechController(IXunfeiTtsService xunfeiTtsService)
        {
            _xunfeiTtsService = xunfeiTtsService;
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
