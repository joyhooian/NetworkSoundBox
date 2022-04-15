using Microsoft.AspNetCore.Http;

namespace NetworkSoundBox.Controllers.Model.Request
{
    public class AddAudioRequest
    {
        public IFormFile file { get; set; }
    }
}
