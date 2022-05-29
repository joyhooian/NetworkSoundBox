﻿using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class CronTask
    {
        public int CronKey { get; set; }
        public string CronReferenceId { get; set; }
        public string UserReferenceId { get; set; }
        public string Weekdays { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public int Volume { get; set; }
        public int Relay { get; set; }
        public string AudioReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
