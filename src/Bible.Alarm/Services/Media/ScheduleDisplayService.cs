using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Database;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class ScheduleDisplayService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IScheduleDisplayService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<string> GetChapterDisplayNameAsync(long scheduleId, bool force = false)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            
            if (!force)
            {
                var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
                if (!playbackService.IsPrepared) return string.Empty;
            }

            await using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

            var schedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .AsNoTracking()
                .Where(x => x.Id == scheduleId)
                .FirstOrDefaultAsync();

            if (schedule?.BibleReadingSchedule == null) return string.Empty;

            await using var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var bookName = await mediaDbContext.BibleBook
                .Where(x => x.BibleTranslation.Code == schedule.BibleReadingSchedule.PublicationCode
                            && x.BibleTranslation.Language.Code == schedule.BibleReadingSchedule.LanguageCode
                            && x.Number == schedule.BibleReadingSchedule.BookNumber)
                .Select(x => x.Name)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (bookName == null) return string.Empty;

            return $"{bookName} {schedule.BibleReadingSchedule.ChapterNumber}";
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened while getting chapter display name for schedule {ScheduleId}", scheduleId);
            return string.Empty;
        }
    }
}

