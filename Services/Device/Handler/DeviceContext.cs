using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceContext : IDeviceContext
    {
        public static readonly Dictionary<string, DeviceHandler> DeviceDict = new();
        public static readonly List<DeviceHandler> DevicePool = new();
        public static readonly Dictionary<string, KeyValuePair<DateTimeOffset, FileContentResult>> FileList = new();
        Dictionary<string, DeviceHandler> IDeviceContext.DeviceDict => DeviceDict;
        List<DeviceHandler> IDeviceContext.DevicePool => DevicePool;
        Dictionary<string, KeyValuePair<DateTimeOffset, FileContentResult>> IDeviceContext.FileList => FileList;
    }
}
