namespace Bible.Alarm.Common.Interfaces.Media;

public interface IPlaybackService
{
    TimeSpan CurrentTrackPosition { get; }
    int CurrentTrackIndex { get; }
    long CurrentlyPlayingScheduleId { get; }
    bool IsPlaying { get; }
    bool IsPrepared { get; }

    Task PrepareRelavantPlaylist();
    Task Play();
    Task Pause();
    Task PlayPrevious();
    Task PlayNext();

    Task PrepareAndPlay(int scheduleId, bool isImmediate);
    Task Dismiss();
}