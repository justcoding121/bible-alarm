namespace Bible.Alarm.Common.Interfaces.Media;

public interface ISchedulePlaybackService
{
    Task PlayScheduleAsync(int scheduleId);
    Task<bool> CanMoveChapterAsync();
}

