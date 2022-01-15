using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Permission
    {
        public int Id { get; set; }
        public string Permission1 { get; set; }
        public int PermissionId { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
