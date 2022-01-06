using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.QRCode.Model
{
    public class WechatQrRequest
    {
        [JsonProperty(propertyName: "scene")]
        public string Scene { get; set; }
        [JsonProperty(propertyName: "page")]
        public string Page { get; set; }
    }
}
