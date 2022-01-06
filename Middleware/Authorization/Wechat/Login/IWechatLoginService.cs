using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.Login
{
    public interface IWechatLoginService
    {
        Task<string> GetWechatOpenId(string code);
    }
}
