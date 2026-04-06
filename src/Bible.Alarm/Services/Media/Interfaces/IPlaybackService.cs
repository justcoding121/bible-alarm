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

    /// <summary>
    /// Stops playback without dispatching Fluxor actions or sending explicit-stop messages.
    /// Used during window teardown to prevent stale dispatches from interfering with a new session
    /// after activity recreation (e.g., Android swipe-out → reopen).
    /// </summary>
    Task StopForTeardownAsync();

    Task ResetAndRetryAsync(int scheduleId);
}

