using MediaManager.Library;
using MediaManager.Player;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Contracts
{
    public interface IPlaybackService 
    {
        TimeSpan CurrentTrackPosition { get; }
        int CurrentTrackIndex { get; }
        long CurrentlyPlayingScheduleId { get; }
        bool IsPlaying { get; }
        bool IsPrepared { get; }

        Task PrepareRelavantPlaylist();
        Task Play();

        Task PrepareAndPlay(long scheduleId, bool isImmediate);
        Task Dismiss();

    }
}
