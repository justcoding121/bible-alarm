namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaybackService : IDisposable
{
    /// <summary>
    /// True when the current session was started as an actual alarm (not notification Play / home Play).
    /// </summary>
    bool IsAlarmPlaybackSession { get; }

    Task PlayAsync();
    Task PauseAsync();
    Task PlayPreviousAsync();
    Task PlayNextAsync();
    Task SeekForwardAsync();
    Task SeekBackwardAsync();
    Task SeekToAsync(TimeSpan position);

    Task PrepareAndPlayAsync(int scheduleId, bool isAlarm);
    Task StopAsync();
    Task ResetAndRetryAsync(int scheduleId);
}

