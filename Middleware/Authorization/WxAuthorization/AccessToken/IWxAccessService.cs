using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.WxAuthorization.AccessToken
{
    public interface IWxAccessService
    {
        Task<WxAccessToken> RequestToken();
    }
}
