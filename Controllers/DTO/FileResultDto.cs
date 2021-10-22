using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetworkSoundBox.Controllers.DTO
{
    public class FileResultDto
    {
        [JsonProperty(propertyName: "status")]
        public string Status { get; }
        [JsonProperty(propertyName: "errMsg")]
        public string ErrorMessage { get; }

        public FileResultDto(string status, string errorMessage)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }
    }
}
