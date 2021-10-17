using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.TextToSpeech
{
    public interface IXunfeiTtsService
    {
        /// <summary>
        /// 文字转语音服务
        /// </summary>
        /// <param name="text"></param>
        /// <param name="vcn"></param>
        /// <param name="speed"></param>
        /// <param name="volume"></param>
        /// <param name="pitch"></param>
        /// <returns></returns>
        Task<List<byte>> GetSpeech(string text, string vcn = "xiaoyan", int speed = 50, int volume = 50, int pitch = 50);
    }
}
