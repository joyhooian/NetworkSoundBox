using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Device
    {
        public int Id { get; set; }
        public string DeviceReferenceId { get; set; }
        public string Sn { get; set; }
        public int Type { get; set; }
        public int IsActived { get; set; }
        public string ActivationKey { get; set; }
        public string Name { get; set; }
        public DateTime? LastOnline { get; set; }
        public string PlaylistReferenceId { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime CreateAt { get; set; }
        public int ActiveCount { get; set; }
    }
}
