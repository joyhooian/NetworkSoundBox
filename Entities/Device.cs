using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Models
{
    [Table("devices")]
    public class Device
    {
        [Key]
        public int id { get; set; }
        public string sn { get; set; }
        public string activation { get; set; }
        [Column("user_id")]
        public int userId { get; set; }
        public string name { get; set; }
        [Column("last_online")]
        public DateTime? lastOnline { get; set; }
        [Column("device_type")]
        public string deviceType { get; set; }
        [NotMapped]
        public bool isOnline { get; set; }
    }
}
