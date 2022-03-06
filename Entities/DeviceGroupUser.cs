using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceGroupUser
    {
        public int Key { get; set; }
        public string DeviceGroupReferenceId { get; set; }
        public string UserReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
