using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using NetworkSoundBox.Services.Device.Handler;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkSoundBox.Services.Message;
using NetworkSoundBox.Middleware.Authorization.Device;
using NetworkSoundBox.Middleware.Hubs;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Middleware.Logger;

namespace NetworkSoundBox.Services.Device.Server
{
    public class DeviceServer : BackgroundService
    {
        private readonly INotificationContext _notificationContext;
        private readonly IDeviceAuthorization _deviceAuthorization;
        private readonly IDeviceContext _deviceContext;
        private readonly ILogger<DeviceServer> _logger;
        private readonly ILogger<DeviceHandler> _subLogger;

        public DeviceServer(
            ILogger<DeviceHandler> sublogger,
            ILogger<DeviceServer> logger,
            INotificationContext notificationContext,
            IDeviceAuthorization deviceAuthorization,
            IDeviceContext deviceContext)
        {
            _subLogger = sublogger;
            _logger = logger;
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
            _logger.LogInformation(LogEvent.ServerHost, "TCP Server is startup for listening");
            //Console.WriteLine("TCP Server is startup for listening");
            while (!cancellationToken.IsCancellationRequested)
            {
                _ = new DeviceHandler(
                    _subLogger,
                    await server.AcceptSocketAsync(),
                    new MessageContext(),
                    _notificationContext,
                    _deviceAuthorization,
                    _deviceContext);
            }
        }
    }
}