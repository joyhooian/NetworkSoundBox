using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Middleware.Logger;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceSupervisor : BackgroundService
    {
        private readonly IDeviceContext _deviceContext;
        private readonly ILogger<DeviceSupervisor> _logger;   

        public DeviceSupervisor(
            ILogger<DeviceSupervisor> logger,
            IDeviceContext deviceContext)
        {
            _deviceContext = deviceContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                var index = 0;
                _logger.LogInformation(LogEvent.ServerHost, "**************************************************************");
                foreach (var (sn, deviceHandler) in _deviceContext.DevicePool)
                {
                    if (deviceHandler.IsHbOverflow)
                    {
                        deviceHandler.Dispose();
                        _deviceContext.DevicePool.Remove(sn);
                        _logger.LogInformation(LogEvent.ServerHost, $"Found zombie [{sn}], kick it out");
                    }
                    else
                    {
                        _logger.LogInformation(LogEvent.ServerHost, $"{index:D5} Device[{sn}]is online @{deviceHandler.IpAddress}:{deviceHandler.Port}");
                        index++;
                    }
                }
                _logger.LogInformation(LogEvent.ServerHost, $"Has {index} devices online");
                _logger.LogInformation(LogEvent.ServerHost, "**************************************************************");
                Thread.Sleep(10_000);
            }
            KillAll();
        }

        private void KillAll()
        {
            foreach (var (sn, deviceHandler) in _deviceContext.DevicePool)
            {
                deviceHandler.Dispose();
            }
        }
    }
}