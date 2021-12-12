using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Authorization.WxAuthorization.QRCode.DTO
{
    public class WxLoginQRDto
    {
        public string LoginKey { get; set; }
        public FileContentResult QRCode { get; set; }
        public bool Success { get; set; }
    }
}
