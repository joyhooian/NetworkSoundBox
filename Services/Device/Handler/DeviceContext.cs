using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceContext : IDeviceContext
    {
        private static readonly Dictionary<string, DeviceHandler> _devicePool = new();
        private static readonly Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> _fileList = new();
        private static readonly Dictionary<string, AudioTrxModel> _audioDict = new();
        private static FileContentResult _fileContentResultTest;

        Dictionary<string, DeviceHandler> IDeviceContext.DevicePool => _devicePool;
        Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> IDeviceContext.FileList => _fileList;
        Dictionary<string, AudioTrxModel> IDeviceContext.AudioDict => _audioDict;
        FileContentResult IDeviceContext.FileContentResult_Test { get => _fileContentResultTest; set => _fileContentResultTest = value; }
    }
}
