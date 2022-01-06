using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Controllers.Model
{
    public class GetDevicesCustomerResponse
    {
        public string Sn { get; set; }
        public string DeviceType { get; set; }
        public string Name { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastOnline { get; set; }
    }
}
