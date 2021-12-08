using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Device
    {
        public uint Id { get; set; }
        public string Sn { get; set; }
        public string DeviceType { get; set; }
        public sbyte Activation { get; set; }
        public string ActivationKey { get; set; }
        public uint UserId { get; set; }
        public string Name { get; set; }
        public int? GroupId { get; set; }
        public DateTime? LastOnline { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual DeviceGroup Group { get; set; }
        public virtual User User { get; set; }
    }
}
