using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Controllers.DTO
{
    public class OverallDto
    {
        public int UserCount { get; set; }
        public int DeviceCount { get; set; }
        public int ActivedCount { get; set; }
        public int OnlineCount { get; set; }
    }
}
