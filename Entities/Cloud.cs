using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class Cloud
    {
        public int CloudKey { get; set; }
        public string CloudReferenceId { get; set; }
        public string UserReferenceId { get; set; }
        public int Capacity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
