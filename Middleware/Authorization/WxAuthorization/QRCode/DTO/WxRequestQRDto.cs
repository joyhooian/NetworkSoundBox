using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Middleware.Authorization.WxAuthorization.QRCode.DTO
{
    public class WxRequestQRDto
    {
        [JsonProperty(propertyName: "scene")]
        public string Scene { get; set; }
    }
}
