using NetworkSoundBox.Models;
using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model
{
    public class GetDeviceGroupDevicesResponse
    {
        public List<DeviceModel> DeviceFromGroup { get; set; }
        public List<DeviceModel> DeviceExcluded { get; set; }
    }
}
