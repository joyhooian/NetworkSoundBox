using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class UserDevice
    {
        public int Id { get; set; }
        public string UserRefrenceId { get; set; }
        public string DeviceRefrenceId { get; set; }
        public int Permission { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
