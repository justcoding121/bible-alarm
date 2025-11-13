using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class ScheduleDisplayService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IScheduleDisplayService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<string> GetChapterDisplayNameAsync(int scheduleId, bool force = false)
    {
        return await GetChapterDisplayNameForBibleReadingAsync(scheduleId, null, force);
    }

    public async Task<string> GetChapterDisplayNameForBibleReadingAsync(int scheduleId, BibleReadingSchedule bibleReadingSchedule, bool force = false)
    {
        try
        {
            var scheduleToUse = bibleReadingSchedule;

            // If bibleReadingSchedule is not provided, load it from database
            if (scheduleToUse == null)
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
                scheduleToUse = schedule.BibleReadingSchedule;
            }

            if (scheduleToUse == null) return string.Empty;

            using var mediaScope = _scopeFactory.CreateScope();
            await using var mediaDbContext = mediaScope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var bookName = await mediaDbContext.BibleBook
                .Where(x => x.BibleTranslation.Code == scheduleToUse.PublicationCode
                            && x.BibleTranslation.Language.Code == scheduleToUse.LanguageCode
                            && x.Number == scheduleToUse.BookNumber)
                .Select(x => x.Name)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (bookName == null) return string.Empty;

            return $"{bookName} {scheduleToUse.ChapterNumber}";
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened while getting chapter display name for schedule {ScheduleId}", scheduleId);
            return string.Empty;
        }
    }
}

