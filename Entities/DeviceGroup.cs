using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceGroup
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public string DeviceGroupReferenceId { get; set; }
        public int UsingStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
