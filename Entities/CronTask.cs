using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class CronTask
    {
        public int Key { get; set; }
        public string CronReferenceId { get; set; }
        public string Weekdays { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Volume { get; set; }
        public int Relay { get; set; }
        public int Audio { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
