using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceGroupDevice
    {
        public int Key { get; set; }
        public string DeviceReferenceId { get; set; }
        public string DeviceGroupReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
