namespace Bible.Alarm.Services.Media.Interfaces;

public interface ISchedulePlaybackService
{
    Task PlayScheduleAsync(int scheduleId);
    Task<bool> CanMoveChapterAsync(int scheduleId);
}

