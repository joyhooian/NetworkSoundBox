using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Playlist
    {
        public int PlaylistKey { get; set; }
        public string PlaylistReferenceId { get; set; }
        public string UserReferenceId { get; set; }
        public string PlaylistName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
