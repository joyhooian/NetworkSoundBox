using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.DTO
{
    public class ConnectionNotifyDto
    {
        public string Sn { get; set; }
        public string DeviceType { get; set; }
        public DateTime LastOnline { get; set; }
    }
}
