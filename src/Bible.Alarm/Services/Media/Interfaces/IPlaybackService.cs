namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaybackService
{
    Task PlayAsync();
    Task PauseAsync();
    Task PlayPreviousAsync();
    Task PlayNextAsync();
    Task SeekForwardAsync();
    Task SeekBackwardAsync();

    Task PrepareAndPlayAsync(int scheduleId, bool isAlarm);
    Task StopAsync();
}

