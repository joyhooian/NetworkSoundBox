using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken.Model
{
    public class GetWechatAccessTokenResponse
    {
        [JsonProperty(propertyName: "access_token")]
        public string AccessToken { get; set; }
        [JsonProperty(propertyName: "expires_in")]
        public int Expiration { get; set; }
        [JsonProperty(propertyName: "errcode")]
        public int ErrorCode { get; set; }
        [JsonProperty(propertyName: "errmsg")]
        public string ErrorMessage { get; set; }
    }
}
