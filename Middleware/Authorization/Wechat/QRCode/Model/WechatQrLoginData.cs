using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.QRCode.Model
{
    public class WechatQrLoginData
    {
        public string LoginKey { get; set; }
        public byte[] QRCode { get; set; }
    }
}
