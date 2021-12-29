using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceSupervisor : BackgroundService
    {
        private readonly IDeviceContext _deviceContext;

        public DeviceSupervisor(IDeviceContext deviceContext)
        {
            _deviceContext = deviceContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                var index = 0;
                foreach (var (sn, deviceHandler) in _deviceContext.DevicePool)
                {
                    if (deviceHandler.IsHbOverflow)
                    {
                        deviceHandler.Dispose();
                        _deviceContext.DevicePool.Remove(sn);
                        Console.WriteLine($"发现僵尸设备[{sn}], 已踢出设备表");
                    }
                    else
                    {
                        Console.WriteLine($"{index:D5}: [{sn}]设备在线 @{deviceHandler.IpAddress}:{deviceHandler.Port}");
                        index++;
                    }
                }
                Console.WriteLine($"{DateTime.Now.ToLocalTime():g} 当前共{index}台设备在线");
                Thread.Sleep(1000 * 10);
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