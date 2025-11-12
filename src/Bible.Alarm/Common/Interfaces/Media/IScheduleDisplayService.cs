using System.Threading.Tasks;

namespace Bible.Alarm.Common.Interfaces.Media;

public interface IScheduleDisplayService
{
    Task<string> GetChapterDisplayNameAsync(long scheduleId, bool force = false);
}

