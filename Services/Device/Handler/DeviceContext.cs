using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.Model;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceContext : IDeviceContext
    {
        private static readonly Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> _fileList = new();
        private static readonly Dictionary<string, AudioTrxModel> _audioDict = new();
        private static FileContentResult _fileContentResultTest;
        private static readonly ConcurrentDictionary<string, DeviceHandler> _deviePoolConCurrent = new();

        Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> IDeviceContext.FileList => _fileList;
        Dictionary<string, AudioTrxModel> IDeviceContext.AudioDict => _audioDict;
        FileContentResult IDeviceContext.FileContentResult_Test { get => _fileContentResultTest; set => _fileContentResultTest = value; }
        ConcurrentDictionary<string, DeviceHandler> IDeviceContext.DevicePoolConCurrent => _deviePoolConCurrent;
    }
}
