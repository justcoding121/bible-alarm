#nullable enable

using Bible.Alarm.Services.Scheduler.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Helpers;

/// <summary>
/// Manages periodic background tasks for Windows (similar to Android JobScheduler).
/// - SchedulerService: Runs every 30 minutes (like Android SchedulerJob)
/// </summary>
public sealed class WindowsPeriodicBackgroundTasks : IDisposable
{
    private readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;

    // Periodic timers for Windows background tasks
    private PeriodicTimer? schedulerTimer; // Runs every 30 minutes (like Android SchedulerJob)
    private CancellationTokenSource? timersCancellationTokenSource;
    private readonly object timersLock = new();
    private bool isDisposed;

    public WindowsPeriodicBackgroundTasks(ILogger logger, IServiceProvider serviceProvider)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Starts periodic background tasks for Windows.
    /// Runs tasks immediately on start, then continues with periodic schedule.
    /// </summary>
    public void Start()
    {
        lock (timersLock)
        {
            if (isDisposed || timersCancellationTokenSource != null)
            {
                // Timers already started or service is disposed
                return;
            }

            timersCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = timersCancellationTokenSource.Token;

            // Run scheduler task immediately on start
            _ = RunSchedulerTaskOnceAsync(cancellationToken);
            // Then start periodic timer (every 30 minutes, like Android SchedulerJob)
            schedulerTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            _ = RunSchedulerTaskAsync(schedulerTimer, cancellationToken);

            logger.Information("Started periodic background tasks for Windows: Scheduler (30 min) - running immediately on start");
        }
    }

    /// <summary>
    /// Runs the scheduler task once immediately on app start.
    /// </summary>
    private async Task RunSchedulerTaskOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.Debug("Running scheduler task immediately on app start");
            var schedulerService = serviceProvider.GetRequiredService<ISchedulerService>();
            await schedulerService.HandleAsync();
        }
        catch (Exception e)
        {
            logger.Error(e, "Error running scheduler task on app start");
        }
    }

    /// <summary>
    /// Runs the scheduler task periodically (every 30 minutes, like Android SchedulerJob)
    /// </summary>
    private async Task RunSchedulerTaskAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    logger.Debug("Windows periodic scheduler task running (every 30 minutes)");
                    var schedulerService = serviceProvider.GetRequiredService<ISchedulerService>();
                    await schedulerService.HandleAsync();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error in Windows periodic scheduler task");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when timer is stopped
            logger.Debug("Windows scheduler timer stopped");
        }
        catch (Exception e)
        {
            logger.Error(e, "Unexpected error in Windows scheduler timer");
        }
    }


    public void Dispose()
    {
        lock (timersLock)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            // Stop and dispose timers
            timersCancellationTokenSource?.Cancel();
            timersCancellationTokenSource?.Dispose();
            timersCancellationTokenSource = null;

            schedulerTimer?.Dispose();
            schedulerTimer = null;

            logger.Debug("Stopped and disposed Windows periodic background task timers");
        }
    }
}
