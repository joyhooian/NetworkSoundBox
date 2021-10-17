using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Services.TextToSpeech.DTO
{
    public class FrameDto
    {
        [JsonProperty(propertyName: "common")]
        public CommonDto Common { get; set; }
        [JsonProperty(propertyName: "business")]
        public BusinessDto Business { get; set; }
        [JsonProperty(propertyName: "data")]
        public DataDto Data { get; set; }
    }

    public class CommonDto
    {
        [JsonProperty(propertyName: "app_id")]
        public string AppId { get; set; }
    }

    public class BusinessDto
    {
        [JsonProperty(propertyName: "aue")]
        public string Aue { get; set; }
        [JsonProperty(propertyName: "sfl")]
        public int Sfl { get; set; }
        [JsonProperty(propertyName: "vcn")]
        public string Vcn { get; set; }
        [JsonProperty(propertyName: "ttp")]
        public string Ttp { get; set; }
        [JsonProperty(propertyName: "speed")]
        public int Speed { get; set; }
        [JsonProperty(propertyName: "volume")]
        public int Volume { get; set; }
        [JsonProperty(propertyName: "pitch")]
        public int Pitch { get; set; }
        [JsonProperty(propertyName: "tte")]
        public string Tte { get; set; }
    }

    public class DataDto
    {
        [JsonProperty(propertyName: "text")]
        public string Text { get; set; }
        [JsonProperty(propertyName: "status")]
        public int Status { get; set; }
    }
}
