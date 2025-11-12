namespace Bible.Alarm.Common.Interfaces.Media;

public interface ISchedulePlaybackService
{
    Task PlayScheduleAsync(long scheduleId);
    Task<bool> CanMoveChapterAsync();
}

