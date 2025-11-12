using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

public class ScheduleSelectionService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IScheduleSelectionService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<AlarmMusic> LoadMusicForSelectionAsync(int scheduleId, bool isNewSchedule, bool musicUpdated, AlarmMusic currentMusic)
    {
        try
        {
            // Get the latest music track if needed
            if (currentMusic == null || (!isNewSchedule && !musicUpdated))
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                return await scheduleDbContext.AlarmMusic
                    .AsNoTracking()
                    .FirstAsync(x => x.AlarmScheduleId == scheduleId);
            }

            return currentMusic;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading music for selection for schedule {ScheduleId}", scheduleId);
            return currentMusic;
        }
    }

    public async Task<BibleReadingSchedule> LoadBibleReadingForSelectionAsync(int scheduleId, bool isNewSchedule, bool bibleReadingUpdated, BibleReadingSchedule currentBibleReading)
    {
        try
        {
            // Get the latest bible track if needed
            if (currentBibleReading == null || (!isNewSchedule && !bibleReadingUpdated))
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                return await scheduleDbContext.BibleReadingSchedules
                    .AsNoTracking()
                    .FirstAsync(x => x.AlarmScheduleId == scheduleId);
            }

            return currentBibleReading;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading bible reading for selection for schedule {ScheduleId}", scheduleId);
            return currentBibleReading;
        }
    }
}

