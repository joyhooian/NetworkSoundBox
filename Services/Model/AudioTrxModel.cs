using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Model
{
    public class AudioTrxModel
    {
        public DateTimeOffset ExpireTime { get; }
        public string Sn { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        private readonly Semaphore _transferSemaphore;
        private bool _transferCpltFlag;

        public AudioTrxModel()
        {
            ExpireTime = DateTimeOffset.Now.AddMinutes(5);
            _transferSemaphore = new Semaphore(0, 1);
        }

        public AudioTrxModel(string sn, string filePath, string fileName)
        {
            ExpireTime = DateTimeOffset.Now.AddMinutes(5);
            Sn = sn;
            FilePath = filePath;
            FileName = fileName;
            _transferSemaphore = new Semaphore(0, 1);
        }

        public Task<bool> Wait()
        {
            _transferSemaphore.WaitOne(1000 * 60 * 5);
            return Task.FromResult(_transferCpltFlag);
        }

        public void TransferCplt(bool result)
        {
            _transferCpltFlag = result;
            _transferSemaphore.Release();
        }
    }
}
