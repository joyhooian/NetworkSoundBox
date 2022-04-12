using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceAudio
    {
        public int DeviceAudioKey { get; set; }
        public string DeviceReferenceId { get; set; }
        public string AudioReferenceId { get; set; }
        public int Index { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
