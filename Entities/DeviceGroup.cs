using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class DeviceGroup
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public string DeviceGroupReferenceId { get; set; }
        public int UsingStatus { get; set; }
        public string PlaylistReferenceId { get; set; }
        public string CronTaskListReferenceId { get; set; }
        public string DelayTaskReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
