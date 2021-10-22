using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NetworkSoundBox.Models;

namespace NetworkSoundBox.Hubs
{
    public class NotificationHub : Hub
    {
        public static HashSet<Client> ClientHashSet { get; } = new HashSet<Client>();
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationHub(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override Task OnConnectedAsync()
        {
            string loginKey = _httpContextAccessor.HttpContext.Request.Query["access_token"];
            Console.WriteLine(loginKey);
            var client = Context.ConnectionId;
            if (loginKey == string.Empty)
            {
                return null;
            }
            RegisterClient(loginKey, client);
            Clients.Client(client).SendAsync("ConnectResponse", "Hello");

            return base.OnConnectedAsync();
        }

        public void RegisterClient(string loginKey, string client)
        {
            ClientHashSet.Add(new Client
            {
                ClientId = client,
                LoginKey = loginKey
            });
        }
    }

    public class Client
    {
        public string ClientId { get; set; }
        public string LoginKey { get; set; }
    }
}
