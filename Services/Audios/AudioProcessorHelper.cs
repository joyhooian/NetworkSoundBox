using Microsoft.EntityFrameworkCore.Internal;
using NetworkSoundBox.Entities;
using System;
using System.Collections.Concurrent;

namespace NetworkSoundBox.Services.Audios
{
    public class AudioProcessorHelper : IAudioProcessorHelper
    {
        private readonly ConcurrentDictionary<string, AudioProcessor> _audioProcessors;

        public AudioProcessorHelper()
        {
            _audioProcessors = new ConcurrentDictionary<string, AudioProcessor>();
        }

        public ConcurrentDictionary<string, AudioProcessor> AudioProcessors => _audioProcessors;

        public AudioProcessor CreateAudioProcessor(string userReferenceId)
        {
            return new AudioProcessor(_audioProcessors, userReferenceId);
        }
    }
}
