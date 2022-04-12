using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Audio
    {
        public int AudioKey { get; set; }
        public string AudioReferenceId { get; set; }
        public string CloudReferenceId { get; set; }
        public string AudioPath { get; set; }
        public string AudioName { get; set; }
        public TimeSpan Duration { get; set; }
        public int Size { get; set; }
        public string IsCached { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
