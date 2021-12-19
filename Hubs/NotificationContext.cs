using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace NetworkSoundBox.Hubs
{
    public class NotificationContext : INotificationContext
    {
        private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
        public static readonly Dictionary<string, string> _clientDict = new();
        public Dictionary<string, string> ClientDict => _clientDict;

        public NotificationContext(
            IHubContext<NotificationHub, INotificationClient> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// 注册临时Client
        /// </summary>
        /// <param name="loginKey"></param>
        /// <param name="clientId"></param>
        public void RegisterTempClient(string loginKey, string clientId)
        {
            _clientDict.Add(loginKey, clientId);
            Console.WriteLine($"临时客户端登陆, key={loginKey}");
        }

        public Task SendClientLogin(string loginKey, string token)
        {
            if (_clientDict.TryGetValue(loginKey, out string clientId))
            {
                _clientDict.Remove(loginKey);
                return _hubContext.Clients.Client(clientId).Login(token);
            }
            return Task.CompletedTask;
        }

        public Task SendDeviceOffline(string openId, string deviceId)
            => _hubContext.Clients.User(openId).DeviceOffline(deviceId);

        public Task SendDeviceOnline(string openId, string deviceId)
            => _hubContext.Clients.User(openId).DeviceOnline(deviceId);

        public Task SendDownloadProgress(string openId, float progress)
            => _hubContext.Clients.User(openId).DownloadProgress(progress.ToString());
    }
}
