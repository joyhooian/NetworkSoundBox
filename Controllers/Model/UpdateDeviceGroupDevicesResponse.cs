using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model
{
    public class UpdateDeviceGroupDevicesResponse
    {
        public int SkippedDeviceCount { get; set; }
        public List<string> SkippedDeviceReferenceIds { get; set; }
    }
}
