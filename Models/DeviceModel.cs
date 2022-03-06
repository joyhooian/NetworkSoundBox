using System;

namespace NetworkSoundBox.Models
{
    public class DeviceModel
    {
        public string DeviceReferenceId { get; set; }
        public string Sn { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public DateTime? LastOnline { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime? CreateAt { get; set; }
    }
}
