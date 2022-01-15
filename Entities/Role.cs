using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Role
    {
        public int Id { get; set; }
        public string RoleName { get; set; }
        public int RoleId { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
