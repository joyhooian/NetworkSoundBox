using System;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken.Model
{
    public class WechatAccessToken
    {
        public string Token { get; set; }
        public DateTimeOffset ExpireAt { get; set; }
    }
}
