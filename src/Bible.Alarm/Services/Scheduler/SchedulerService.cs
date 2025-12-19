using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

public class SchedulerService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IMediaCacheService mediaCacheService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IStorageService storageService) : ISchedulerService, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private static readonly SemaphoreSlim @lock = new(1);

    public async Task ProcessScheduledTasksAsync()
    {
        await HandleAsync();
    }

    public async Task<bool> HandleAsync()
    {
        try
        {
            var downloaded = await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
            {
                try
                {
                    await mediaCacheService.CleanUpAsync();
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happenned inside cleanup task.");
                }

                var downloaded = false;
                var schedules = await alarmScheduleService.GetSchedulesAsync(x => x.IsEnabled, includeMusic: false, includeBibleReading: false, _cancellationTokenSource.Token);
                foreach (var schedule in schedules)
                {
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

                return downloaded;
            }, timeoutMs: 1000);

            if (!downloaded)
            {
                logger.Warning("Failed to acquire lock for scheduler task (timeout). Db directory: {CacheRoot}", storageService.CacheRoot);
                return false;
            }

            return downloaded;
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to process scheduler task. Db directory: {CacheRoot}", storageService.CacheRoot);
            return false;
        }
    }

    /// <summary>
    /// Reschedules the next occurrence of a recurring alarm.
    /// This is necessary for WinUI 3 which doesn't support UWP background tasks.
    /// </summary>
    public async Task RescheduleNextOccurrenceAsync(int scheduleId)
    {
        try
        {
            // Load the schedule with all includes
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, includeMusic: true, includeBibleReading: true);

            if (schedule != null && schedule.IsEnabled)
            {
                // Check if notification is already scheduled (shouldn't be, but check anyway)
                var isScheduled = await notificationService.IsScheduledAsync(scheduleId);
                if (!isScheduled)
                {
                    // Reschedule the next occurrence
                    await alarmService.Create(schedule);
                    logger.Information("Rescheduled next occurrence for schedule {ScheduleId}", scheduleId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error rescheduling next occurrence for schedule {ScheduleId}", scheduleId);
        }
    }

    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // Dispose static semaphore
        try
        {
            @lock.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore if already disposed
            logger.Warning(ex, "Error disposing semaphore, may already be disposed");
        }

        // Note: alarmScheduleService, mediaCacheService, alarmService, notificationService, and storageService are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}

