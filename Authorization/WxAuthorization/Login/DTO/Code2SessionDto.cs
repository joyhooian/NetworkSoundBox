﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Authorization.WxAuthorization.Login.DTO
{
    public class Code2SessionDto
    {
        [JsonProperty(propertyName: "openid")]
        public string OpenId { get; set; }
        [JsonProperty(propertyName: "seesion_key")]
        public string SessionKey { get; set; }
        [JsonProperty(propertyName: "errcode")]
        public int ErrorCode { get; set; }
        [JsonProperty(propertyName: "errmsg")]
        public string ErrorMessage { get; set; }
    }
}
