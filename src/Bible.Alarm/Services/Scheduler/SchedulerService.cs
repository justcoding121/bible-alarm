using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

public sealed class SchedulerService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IMediaCacheService mediaCacheService,
    IAlarmService alarmService,
    INotificationService notificationService,
    IStorageService storageService) : ISchedulerService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private static readonly SemaphoreSlim @lock = new(1);

    // Increased timeout from 1000ms to 10000ms to allow for bootstrap operations
    private const int LockTimeoutMs = 10000;

    public async Task ProcessScheduledTasksAsync() => await HandleAsync();

    public async Task<bool> HandleAsync()
    {
        try
        {
            // Wait for bootstrap to complete before attempting scheduler operations
            // This prevents lock timeouts when bootstrap is still running database operations
            if (!BootstrapHelper.IsBootstrapCompleted())
            {
                logger.Debug("Bootstrap not completed yet, waiting for bootstrap before running scheduler");
                try
                {
                    await BootstrapHelper.WaitForBootstrapAsync();
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to wait for bootstrap completion, proceeding with scheduler anyway");
                }
            }

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
                var schedules = await alarmScheduleService.GetSchedulesAsync(x => x.IsEnabled, includeMusic: false, includeBiblePublication: false, cancellationTokenSource.Token);
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
            }, timeoutMs: LockTimeoutMs);

            if (!downloaded)
            {
                logger.Warning("Failed to acquire lock for scheduler task (timeout after {TimeoutMs}ms). Db directory: {CacheRoot}", LockTimeoutMs, storageService.CacheRoot);
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
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, includeMusic: true, includeBiblePublication: true);

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

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
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

