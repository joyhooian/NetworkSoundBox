using System;

namespace NetworkSoundBox.Models
{
    public class AudioModel
    {
        public string AudioName { get; set; }
        public string AudioType { get; set; }
        public TimeSpan Duration { get; set; }
        public int Size { get; set; }
    }
}
