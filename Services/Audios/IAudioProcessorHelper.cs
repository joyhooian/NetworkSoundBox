using NetworkSoundBox.Entities;
using System.Collections.Concurrent;

namespace NetworkSoundBox.Services.Audios
{
    public interface IAudioProcessorHelper
    {
        ConcurrentDictionary<string, AudioProcessor> AudioProcessors { get; }
        AudioProcessor CreateAudioProcessor(string userReferenceId);
    }
}