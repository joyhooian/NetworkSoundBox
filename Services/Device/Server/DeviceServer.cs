using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using NetworkSoundBox.Authorization.Device;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Services.Device.Handler;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkSoundBox.Services.Message;

namespace NetworkSoundBox.Services.Device.Server
{
    public class DeviceServer : BackgroundService
    {
        private readonly INotificationContext _notificationContext;
        private readonly IDeviceAuthorization _deviceAuthorization;
        private readonly IDeviceContext _deviceContext;

        public DeviceServer(
            INotificationContext notificationContext,
            IDeviceAuthorization deviceAuthorization,
            IDeviceContext deviceContext)
        {
            _notificationContext = notificationContext;
            _deviceAuthorization = deviceAuthorization;
            _deviceContext = deviceContext;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            var listenAddress = IPAddress.Parse("0.0.0.0");
            TcpListener server = new(listenAddress, 10808);
            server.Start();
            Console.WriteLine("TCP Server is startup for listening");
            while (!cancellationToken.IsCancellationRequested)
            {
                _ = new DeviceHandler(await server.AcceptSocketAsync(),
                    new MessageContext(),
                    _notificationContext,
                    _deviceAuthorization,
                    _deviceContext);
            }
        }
    }
}