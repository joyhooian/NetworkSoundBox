using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DelayTask
    {
        public int Key { get; set; }
        public string DelayReferenceId { get; set; }
        public int DelayTime { get; set; }
        public int Volume { get; set; }
        public int Relay { get; set; }
        public int Audio { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
