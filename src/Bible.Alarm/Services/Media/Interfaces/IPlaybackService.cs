namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaybackService : IDisposable
{
    Task PlayAsync();
    Task PauseAsync();
    Task PlayPreviousAsync();
    Task PlayNextAsync();
    Task SeekForwardAsync();
    Task SeekBackwardAsync();
    Task SeekToAsync(TimeSpan position);

    Task PrepareAndPlayAsync(int scheduleId, bool isAlarm);
    Task StopAsync();
}

