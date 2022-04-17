using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.Audios;
using System.Collections.Concurrent;
using System.Threading;

namespace NetworkSoundBox.Services.DTO
{
    public class AudioProcessDto
    {
        public AudioProcessReultToken AudioProcessToken { get; set; }
        public IFormFile FormFile { get; set; }
        public string RootPath { get; set; }
    }

    public class AudioProcessReultToken
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string ResponseMesssage { get; set; }
        public readonly Semaphore Semaphore = new(0, 1);

        public IActionResult WaitResult()
        {
            Semaphore.WaitOne();
            return Success ? new OkObjectResult(ResponseMesssage) : new BadRequestObjectResult(ErrorMessage);
        }
    }
}
