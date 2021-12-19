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

namespace NetworkSoundBox.Services.Device.Server
{
    public class ServerService : BackgroundService
    {
        private readonly INotificationContext _notificationContext;
        private readonly IDeviceAuthorization _deviceAuthorization;

        public ServerService(INotificationContext notificationContext, IDeviceAuthorization deviceAuthorization)
        {
            _notificationContext = notificationContext;
            _deviceAuthorization = deviceAuthorization;
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            IPAddress listeningAddr = IPAddress.Parse("0.0.0.0");
            TcpListener server = new(listeningAddr, 10808);
            server.Start();
            while (true)
            {
                Console.WriteLine("[Test] Waiting for a connection...");
                new DeviceHandler(server.AcceptSocket(), _notificationContext, _deviceAuthorization);
            }
        }
    }
}
