using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceContext : IDeviceContext
    {
        public static readonly Dictionary<string, DeviceHandler> DevicePool = new();
        public static readonly Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> FileList = new();
        public static FileContentResult FileContentResult_Test;

        Dictionary<string, DeviceHandler> IDeviceContext.DevicePool => DevicePool;
        Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> IDeviceContext.FileList => FileList;
        FileContentResult IDeviceContext.FileContentResult_Test { get => FileContentResult_Test; set => FileContentResult_Test = value; }
    }
}
