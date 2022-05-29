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
                if (_deviceContext.DevicePool.TryGetValue(eventMsg.Sn, out var handler))
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
                            });
                            handler.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken));
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
        public OperationType OperationType { get; set; }
        public string Sn { get; set; }
        public string FileName { get; set; }
        public string AudioPath { get; set; }
        public int DeviceAudioKey { get; set; }
        public string DeviceReferenceId { get; set; }
    }

    public enum OperationType
    {
        Add,
        Delete
    }
}

