using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Model
{
    public class AudioTrxModel
    {
        public string FileName { get; set; }
        public string AudioPath { get; set; }
        public int? DeviceAudioKey { get; set; }
        public string DeviceReferenceId { get; set; }
        public string Sn { get; set; }
        public string AudioReferenceId { get; set; }
        private readonly Semaphore _transferSemaphore;
        private bool _transferCpltFlag;

        public AudioTrxModel()
        {
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
