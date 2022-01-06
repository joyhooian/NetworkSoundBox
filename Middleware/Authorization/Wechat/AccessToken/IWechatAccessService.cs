using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken.Model;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken
{
    public interface IWechatAccessService
    {
        Task<WechatAccessToken> GetWechatAccessToken();
    }
}
