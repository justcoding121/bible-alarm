namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaybackService
{
    TimeSpan CurrentTrackPosition { get; }
    int CurrentTrackIndex { get; }
    int CurrentlyPlayingScheduleId { get; }
    bool IsPlaying { get; }
    bool IsPrepared { get; }

    Task PlayAsync();
    Task PauseAsync();
    Task PlayPreviousAsync();
    Task PlayNextAsync();

    Task PrepareAndPlayAsync(int scheduleId, bool isAlarm);
    Task StopAsync();
}

