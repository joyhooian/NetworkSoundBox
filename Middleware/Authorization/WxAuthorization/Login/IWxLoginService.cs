using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.WxAuthorization.Login
{
    public interface IWxLoginService
    {
        Task<string> Code2Session(string code);
    }
}
