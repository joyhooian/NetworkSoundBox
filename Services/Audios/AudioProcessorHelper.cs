using Microsoft.EntityFrameworkCore.Internal;
using NetworkSoundBox.Entities;
using System;
using System.Collections.Concurrent;

namespace NetworkSoundBox.Services.Audios
{
    public class AudioProcessorHelper : IAudioProcessorHelper
    {
        private readonly ConcurrentDictionary<string, AudioProcessor> _audioProcessors;
        private readonly BlockingCollection<AudioSyncEvent> _processingQueue;

        public AudioProcessorHelper()
        {
            _audioProcessors = new ConcurrentDictionary<string, AudioProcessor>();
            _processingQueue = new BlockingCollection<AudioSyncEvent>();
        }

        public ConcurrentDictionary<string, AudioProcessor> AudioProcessors => _audioProcessors;
        public BlockingCollection<AudioSyncEvent> ProcessingQueue => _processingQueue;

        public void AddAudioSyncEvent(AudioSyncEvent syncEvent)
        {
            _processingQueue.Add(syncEvent);
        }

        public AudioProcessor CreateAudioProcessor(string userReferenceId)
        {
            return new AudioProcessor(_audioProcessors, userReferenceId);
        }
    }
}
