using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceTask
    {
        public int DeviceTaskKey { get; set; }
        public string DeviceReferenceId { get; set; }
        public string TaskReferenceId { get; set; }
        public int TaskTypeKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
