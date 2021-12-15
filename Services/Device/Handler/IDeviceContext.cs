using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public interface IDeviceContext
    {
        public Dictionary<string, DeviceHandler> DevicePool { get; }
        public Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> FileList { get; }
    }
}
