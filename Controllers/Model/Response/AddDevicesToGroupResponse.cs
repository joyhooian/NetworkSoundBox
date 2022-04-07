using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model.Response
{
    public class AddDevicesToGroupResponse
    {
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> SkippedDevices { get; set; }
    }
}
