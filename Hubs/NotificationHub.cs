using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NetworkSoundBox.Models;

namespace NetworkSoundBox.Hubs
{
    public class NotificationHub : Hub
    {
        private static readonly List<string> OnlineUser = new List<string>();

        public override Task OnConnectedAsync()
        {
            var user = Context.ConnectionId;
            OnlineUser.Add(user);

            Clients.Client(user).SendAsync("ConnectResponse", "Hello");

            return base.OnConnectedAsync();
        }

        public async Task NotiDeviceStatus()
        {
            var user = OnlineUser.FirstOrDefault();
            if (user != null)
            {
                await Clients.Client(user).SendAsync("Notification");
            }
        }
    }
}
