using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model
{
    public class RemoveDevicesFromGroupResponse
    {
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> SkippedDevices { get; set; }
    }
}
