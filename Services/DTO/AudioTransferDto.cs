using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.DTO
{
    public class AudioTransferDto
    {
        public DateTimeOffset ExpireTime { get; }
        public string Sn { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string User { get; set; }
        private readonly Semaphore _transferSemaphore;
        private bool _transferCpltFlag;

        public AudioTransferDto()
        {
            ExpireTime = DateTimeOffset.Now.AddMinutes(5);
            _transferSemaphore = new Semaphore(0, 1);
        }

        public AudioTransferDto(string sn, string filePath, string user, string fileName)
        {
            ExpireTime = DateTimeOffset.Now.AddMinutes(5);
            Sn = sn;
            FilePath = filePath;
            FileName = fileName;
            User = user;
            _transferSemaphore = new Semaphore(0, 1);
        }

        public Task<bool> Wait()
        {
            _transferSemaphore.WaitOne(1000 * 60 * 5);
            return Task.FromResult(_transferCpltFlag);
        }

        public void TransferCplt(bool result)
        {
            _transferSemaphore.Release();
            _transferCpltFlag = result;
        }
    }
}
