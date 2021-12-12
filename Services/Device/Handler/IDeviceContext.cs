using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public interface IDeviceContext
    {
        public Dictionary<string, DeviceHandler> DeviceDict { get; }
        public List<DeviceHandler> DevicePool { get; }
        public Dictionary<string, KeyValuePair<DateTimeOffset, FileContentResult>> FileList { get; }
    }
}
