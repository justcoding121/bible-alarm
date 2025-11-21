namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaybackService
{
    int? CurrentScheduleId { get; }
    bool IsPreparingOrPlaying { get; }

    Task PlayAsync();
    Task PauseAsync();
    Task PlayPreviousAsync();
    Task PlayNextAsync();

    Task PrepareAndPlayAsync(int scheduleId, bool isAlarm);
    Task StopAsync();
}

