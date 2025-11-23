using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

public class SchedulerService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IMediaCacheService mediaCacheService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IStorageService storageService)
    : ISchedulerService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private static readonly SemaphoreSlim Lock = new(1);

    public async Task ProcessScheduledTasksAsync()
    {
        await HandleAsync();
    }

    public async Task<bool> HandleAsync()
    {
        var downloaded = false;
        if (await Lock.WaitAsync(1000))
            try
            {
                try
                {
                    await mediaCacheService.CleanUpAsync();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "An error happenned inside cleanup task.");
                }

                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                var schedules = await scheduleDbContext.AlarmSchedules.Where(x => x.IsEnabled).ToListAsync();
                foreach (var schedule in schedules)
                    if (!await notificationService.IsScheduledAsync(schedule.Id))
                    {
                        downloaded = true;
                        await alarmService.Create(schedule);
                    }
                    else
                    {
                        downloaded = await mediaCacheService.SetupAlarmCacheAsync(schedule.Id);
                    }
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to process scheduler task. Db directory: {storageService.CacheRoot}");
            }
            finally
            {
                try
                {
                    Lock.Release();
                }
                catch (ObjectDisposedException e)
                {
                    _logger.Error(e, "SchedulerService: @lock disposed error.");
                }
            }

        return downloaded;
    }

    /// <summary>
    /// Reschedules the next occurrence of a recurring alarm.
    /// This is necessary for WinUI 3 which doesn't support UWP background tasks.
    /// </summary>
    public async Task RescheduleNextOccurrenceAsync(int scheduleId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            var alarmService = scope.ServiceProvider.GetRequiredService<IAlarmService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // Load the schedule with all includes
            var schedule = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Include(x => x.Music)
                .FirstOrDefaultAsync(x => x.Id == scheduleId);

            if (schedule != null && schedule.IsEnabled)
            {
                // Check if notification is already scheduled (shouldn't be, but check anyway)
                var isScheduled = await notificationService.IsScheduledAsync(scheduleId);
                if (!isScheduled)
                {
                    // Reschedule the next occurrence
                    await alarmService.Create(schedule);
                    _logger.Information("Rescheduled next occurrence for schedule {ScheduleId}", scheduleId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error rescheduling next occurrence for schedule {ScheduleId}", scheduleId);
        }
    }

    public void Dispose()
    {
        // Note: DbContext is now created via IServiceScopeFactory and disposed by the scope
        // mediaCacheService, alarmService, notificationService, and storageService are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}

