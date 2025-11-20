namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaybackService
{
    TimeSpan CurrentTrackPosition { get; }
    int CurrentTrackIndex { get; }
    int CurrentlyPlayingScheduleId { get; }
    bool IsPlaying { get; }
    bool IsPrepared { get; }

    Task Play();
    Task Pause();
    Task PlayPrevious();
    Task PlayNext();

    Task PrepareAndPlay(int scheduleId, bool isImmediate);
    Task Dismiss();
}

