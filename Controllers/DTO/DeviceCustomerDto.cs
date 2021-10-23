using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Controllers.DTO
{
    public class DeviceCustomerDto
    {
        public string Sn { get; set; }
        public string DeviceType { get; set; }
        public string Name { get; set; }
        public int GroupId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastOnline { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
