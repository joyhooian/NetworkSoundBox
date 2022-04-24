using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class PlaylistAudio
    {
        public int PlaylistAudioKey { get; set; }
        public string PlaylistReferenceId { get; set; }
        public string AudioReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
