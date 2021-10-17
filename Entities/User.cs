using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public int UserId { get; set; }
        public string Name { get; set; }
        [Column("tel_num")]
        public string TelNum { get; set; }
        [Column("email")]
        public string Email { get; set; }
        [Column("openid")]
        public string OpenId { get; set; }
        [Column("role")]
        public string Role { get; set; }
        [NotMapped]
        public string ConnectionId { get; set; }
    }
}
