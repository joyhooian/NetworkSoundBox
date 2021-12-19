using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceContext : IDeviceContext
    {
        public static readonly Dictionary<string, DeviceHandler> _devicePool = new();
        public static readonly Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> _fileList = new();
        public static readonly Dictionary<string, AudioTransferDto> _audioDict = new();
        public static FileContentResult _fileContentResualtTest;

        Dictionary<string, DeviceHandler> IDeviceContext.DevicePool => _devicePool;
        Dictionary<string, KeyValuePair<Semaphore, FileContentResult>> IDeviceContext.FileList => _fileList;
        Dictionary<string, AudioTransferDto> IDeviceContext.AudioDict => _audioDict;
        FileContentResult IDeviceContext.FileContentResult_Test { get => _fileContentResualtTest; set => _fileContentResualtTest = value; }

    }
}
