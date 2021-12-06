﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.DTO
{
    public class TimeSettingDto
    {
        public string Sn { get; set; } 
        public int Index { get; set; }
        public List<int> Weekdays { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Volumn { get; set; }
        public bool Relay { get; set; }
        public int Audio { get; set; }
    }
}
