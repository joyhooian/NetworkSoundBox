﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.WxAuthorization.QRCode.DTO
{
    public class WxErrorQRDto
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}