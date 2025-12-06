namespace Bible.Alarm.Services.Media.Interfaces;

public interface ISchedulePlaybackService : IDisposable
{
    Task PlayScheduleAsync(int scheduleId);
    Task<bool> CanMoveChapterAsync(int scheduleId);
}

