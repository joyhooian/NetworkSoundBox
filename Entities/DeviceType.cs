using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceType
    {
        public int Id { get; set; }
        public string DeviceType1 { get; set; }
        public int DeviceTypeId { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
