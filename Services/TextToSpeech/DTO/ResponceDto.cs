using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Services.TextToSpeech.DTO
{
    public class ResponceDto
    {
        [JsonProperty(propertyName: "code")]
        public int Code { get; set; }
        [JsonProperty(propertyName: "message")]
        public string Message { get; set; }
        [JsonProperty(propertyName: "data")]
        public ResponseDataDto ResponseData { get; set; }
    }

    public class ResponseDataDto
    {
        [JsonProperty(propertyName: "audio")]
        public string Audio { get; set; }
        [JsonProperty(propertyName: "status")]
        public int Status { get; set; }
        [JsonProperty(propertyName: "ced")]
        public string Ced { get; set; }
        [JsonProperty(propertyName: "sid")]
        public string Sid { get; set; }
    }
}
