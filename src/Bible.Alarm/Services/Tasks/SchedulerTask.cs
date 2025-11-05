using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Contracts.Scheduler;
using Bible.Alarm.Contracts.Storage;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Services.Infrastructure.Schedule;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Tasks;

public class SchedulerTask(
    ILogger logger,
    ScheduleDbContext scheduleDbContext,
    IMediaCacheService mediaCacheService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IStorageService storageService)
    : ISchedulerService, IDisposable
{
    private readonly ILogger _logger = logger;

    private static readonly SemaphoreSlim Lock = new(1);

    public async Task ProcessScheduledTasks()
    {
        await Handle();
    }

    public async Task<bool> Handle()
    {
        var downloaded = false;
        if (await Lock.WaitAsync(1000))
            try
            {
                try
                {
                    await mediaCacheService.CleanUp();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "An error happenned inside cleanup task.");
                }

                var schedules = await scheduleDbContext.AlarmSchedules.Where(x => x.IsEnabled).ToListAsync();
                foreach (var schedule in schedules)
                    if (!await notificationService.IsScheduled(schedule.Id))
                    {
                        downloaded = true;
                        await alarmService.Create(schedule);
                    }
                    else
                    {
                        downloaded = await mediaCacheService.SetupAlarmCache(schedule.Id);
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
                    _logger.Error(e, "SchedulerTask: @lock disposed error.");
                }
            }

        return downloaded;
    }

    public void Dispose()
    {
        scheduleDbContext.Dispose();
        // Note: mediaCacheService, alarmService, notificationService, and storageService are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}