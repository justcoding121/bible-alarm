using Bible.Alarm.Database;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Database;

public class ScheduleMigrationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IScheduleMigrationService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task MigrateBibleGatewaySchedulesAsync()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

            var alarmSchedules = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Where(x => x.BibleReadingSchedule != null)
                .ToListAsync();

            //bible gateway is not supported anymore due to copyright issues
            var toRemove = alarmSchedules.Where(x =>
                BgSourceHelper.PublicationCodeToNameMappings.Any(y => y.Key == x.BibleReadingSchedule.PublicationCode)).ToList();

            if (toRemove.Any())
            {
                foreach (var item in toRemove)
                {
                    item.BibleReadingSchedule.PublicationCode = "bi12";
                    item.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                }

                await scheduleDbContext.SaveChangesAsync();
                _logger.Information("Migrated {Count} Bible Gateway schedules to default publication code", toRemove.Count);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened while migrating Bible Gateway schedules.");
        }
    }
}

