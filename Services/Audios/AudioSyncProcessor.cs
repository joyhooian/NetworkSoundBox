using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Services.Device.Handler;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Audios
{
    public class AudioSyncProcessor : BackgroundService
    {
        private readonly ILogger<AudioSyncProcessor> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDeviceContext _deviceContext;
        private readonly IAudioProcessorHelper _helper;

        public AudioSyncProcessor(
            ILogger<AudioSyncProcessor> logger,
            IConfiguration configuration,
            IDeviceContext deviceContext,
            IAudioProcessorHelper helper)
        {
            _logger = logger;
            _configuration = configuration;
            _deviceContext = deviceContext;
            _helper = helper;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while(!stoppingToken.IsCancellationRequested)
            {
                var eventMsg = _helper.ProcessingQueue.Take(stoppingToken);
                if (_deviceContext.DevicePoolConCurrent.TryGetValue(eventMsg.Sn, out var handler))
                {
                    switch (eventMsg.OperationType)
                    {
                        case OperationType.Add:
                            var fileToken = Guid.NewGuid().ToString("N")[..8];
                            _deviceContext.AudioDict.Add(fileToken, new Model.AudioTrxModel()
                            {
                                FileName = eventMsg.FileName,
                                AudioPath = eventMsg.AudioPath,
                                DeviceAudioKey = eventMsg.DeviceAudioKey,
                                DeviceReferenceId = eventMsg.DeviceReferenceId,
                                AudioReferenceId = eventMsg.AudioReferenceId,
                                Sn = eventMsg.Sn
                            });
                            Task.Run(() => handler.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken)), stoppingToken);
                            break;
                        case OperationType.Delete:
                        default:
                            break;
                    }
                }
            }
        }
    }

    public class AudioSyncEvent
    {
        public OperationType OperationType { get; init; }
        public string Sn { get; init; }
        public string FileName { get; init; }
        public string AudioPath { get; init; }
        public int DeviceAudioKey { get; init; }
        public string DeviceReferenceId { get; init; }
        public string AudioReferenceId { get; init; }
    }

    public enum OperationType
    {
        Add,
        Delete
    }
}

