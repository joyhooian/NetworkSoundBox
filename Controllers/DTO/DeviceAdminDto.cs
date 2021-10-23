using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Controllers.DTO
{
    public class DeviceAdminDto
    {
        public string Sn { get; set; }
        public string DeviceType { get; set; }
        public bool Activation { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastOnline { get; set; }
    }
}
