using NetworkSoundBox.Middleware.Authorization.Wechat.QRCode.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.QRCode
{
    public interface IWechatQrService
    {
        Task<WechatQrLoginData> GetWechatLoginQrAsync();
    }
}
