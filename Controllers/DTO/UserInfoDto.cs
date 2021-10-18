using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Controllers.DTO
{
    public class UserInfoDto
    {
        [JsonProperty(propertyName: "roles")]
        public List<string> Roles { get; set; }
        [JsonProperty(propertyName: "introduction")]
        public string Introduction { get; set; }
        [JsonProperty(propertyName: "avatar")]
        public string Avatar { get; set; }
        [JsonProperty(propertyName: "name")]
        public string Name { get; set; }
    }
}
