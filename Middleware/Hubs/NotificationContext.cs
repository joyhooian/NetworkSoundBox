using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NetworkSoundBox.Entities;

namespace NetworkSoundBox.Middleware.Hubs
{
    public class NotificationContext : INotificationContext
    {
        public static readonly Dictionary<string, string> _clientDict = new();

        private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        public Dictionary<string, string> ClientDict => _clientDict;

        public NotificationContext(
            IServiceScopeFactory scopeFactory,
            IHubContext<NotificationHub, INotificationClient> hubContext)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
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

        public Task SendDeviceOffline(string sn)
            => _hubContext.Clients.Users(GetUserRefrenceIdList(sn)).DeviceOnline(sn);

        public Task SendDeviceOnline(string sn)
            => _hubContext.Clients.Users(GetUserRefrenceIdList(sn)).DeviceOnline(sn);

        public Task SendDownloadProgress(float progress, string sn)
            => _hubContext.Clients.Users(GetUserRefrenceIdList(sn)).DownloadProgress(progress.ToString());

        private List<string> GetUserRefrenceIdList(string sn)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MySqlDbContext>();
            return (from userDevice in dbContext.UserDevices
                            join device in dbContext.Devices
                            on userDevice.DeviceRefrenceId equals device.DeviceReferenceId
                            join user in dbContext.Users
                            on userDevice.UserRefrenceId equals user.UserRefrenceId
                            where device.Sn == sn ||user.Role.Equals(1)
                            select userDevice.UserRefrenceId).ToList();
        }
    }
}
