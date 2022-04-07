using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model.Request
{
    public class UpdateDeviceGroupDevicesRequest
    {
        public string DeviceGroupReferenceId { get; set; }
        public List<string> DeviceReferenceIds { get; set; }
    }
}
