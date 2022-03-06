using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model
{
    public class RemoveDevicesFromGroupRequest
    {
        public string DeviceGroupReferenceId { get; set; }
        public List<string> Devices { get; set; }
    }
}
