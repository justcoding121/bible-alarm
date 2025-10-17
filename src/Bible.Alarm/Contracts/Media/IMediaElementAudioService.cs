using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Contracts
{
    public interface IMediaElementAudioService : IPlaybackService
    {
        Task SetSource(string source);
        Task Pause();
        Task Stop();
        Task SeekTo(TimeSpan position);
        Task PrepareRelevantPlaylist();
        void Dispose();
    }
}
