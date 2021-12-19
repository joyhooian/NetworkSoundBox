using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NetworkSoundBox.Authorization.Jwt;

namespace NetworkSoundBox.Hubs
{
    public class NotificationHub : Hub<INotificationClient>
    {

        private readonly INotificationContext _notificationContext;

        public NotificationHub(
            INotificationContext notificationContext)
        {
            _notificationContext = notificationContext;
        }


        /// <summary>
        /// 处理客户端登陆事件
        /// </summary>
        /// <returns></returns>
        public override Task OnConnectedAsync()
        {
            var openId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(openId))
            {
                var httpContext = Context.GetHttpContext();
                if (httpContext != null)
                {
                    var loginKey = httpContext.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(loginKey))
                    {
                        _notificationContext.RegisterTempClient(loginKey, Context.ConnectionId);
                        return base.OnConnectedAsync();
                    }
                }
                Context.Abort();
            }
            return base.OnConnectedAsync();
        }
    }
}
