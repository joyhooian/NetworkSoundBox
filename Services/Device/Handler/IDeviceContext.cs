using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public interface IDeviceContext
    {
        public ConcurrentDictionary<string, DeviceHandler> DevicePoolConCurrent { get; }
        public Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> FileList { get; }
        public Dictionary<string, AudioTrxModel> AudioDict { get; }
        public FileContentResult FileContentResult_Test { get; set; }
    }
}
