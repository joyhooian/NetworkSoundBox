using NetworkSoundBox.Models;
using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model.Response
{
    public class GetAudiosResponse
    {
        public List<AudioModel> Audios { get; set; }
    }
}
