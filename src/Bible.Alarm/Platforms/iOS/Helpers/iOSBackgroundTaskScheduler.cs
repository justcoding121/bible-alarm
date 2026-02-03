#nullable enable

using BackgroundTasks;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Foundation;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

internal static class iOSBackgroundTaskScheduler
{
    internal const string SchedulerRefreshTaskIdentifier = "com.jthomas.info.Bible.Alarm.SchedulerRefresh";

    private static readonly ILogger logger = Log.ForContext(typeof(iOSBackgroundTaskScheduler));

    private static volatile bool isRegistered;
    private static readonly object registerLock = new();

    internal static void RegisterAndSchedule()
    {
        Register();
        ScheduleSchedulerRefresh();
    }

    internal static void Register()
    {
        if (isRegistered)
        {
            return;
        }

        lock (registerLock)
        {
            if (isRegistered)
            {
                return;
            }

            try
            {
                var success = BGTaskScheduler.Shared.Register(
                    SchedulerRefreshTaskIdentifier,
                    queue: null,
                    launchHandler: task =>
                    {
                        try
                        {
                            if (task is BGAppRefreshTask refreshTask)
                            {
                                HandleSchedulerRefreshTask(refreshTask);
                                return;
                            }

                            logger.Warning(
                                "iOS BGTask: Unexpected task type for {Identifier}. TaskType={TaskType}",
                                SchedulerRefreshTaskIdentifier,
                                task?.GetType().FullName ?? "(null)");

                            task?.SetTaskCompleted(false);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "iOS BGTask: Error handling task for {Identifier}", SchedulerRefreshTaskIdentifier);
                            try
                            {
                                task?.SetTaskCompleted(false);
                            }
                            catch (Exception ex2)
                            {
                                logger.Error(ex2, "iOS BGTask: Error completing task after handler exception. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
                            }
                        }
                    });

                isRegistered = success;
                if (!success)
                {
                    logger.Warning("Failed to register iOS BGAppRefreshTask. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
                }
                else
                {
                    logger.Information("Registered iOS BGAppRefreshTask. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to register iOS BGAppRefreshTask. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
            }
        }
    }

    internal static void ScheduleSchedulerRefresh(TimeSpan? earliestDelay = null)
    {
        try
        {
            var delay = earliestDelay ?? TimeSpan.FromMinutes(30);
            var request = new BGAppRefreshTaskRequest(SchedulerRefreshTaskIdentifier)
            {
                EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(delay.TotalSeconds)
            };

            BGTaskScheduler.Shared.Submit(request, out var error);
            if (error != null)
            {
                logger.Warning(
                    "Failed to submit iOS BGAppRefreshTaskRequest. Identifier={Identifier}, Error={Error}",
                    SchedulerRefreshTaskIdentifier,
                    error);
            }
            else
            {
                logger.Debug(
                    "Submitted iOS BGAppRefreshTaskRequest. Identifier={Identifier}, EarliestDelayMinutes={Minutes}",
                    SchedulerRefreshTaskIdentifier,
                    delay.TotalMinutes);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error submitting iOS BGAppRefreshTaskRequest. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
        }
    }

    private static void HandleSchedulerRefreshTask(BGAppRefreshTask task)
    {
        // Always queue the next run as early as possible to keep the system scheduling active.
        // iOS will decide the actual run time.
        ScheduleSchedulerRefresh();

        var cancellationTokenSource = new CancellationTokenSource();
        task.ExpirationHandler = () =>
        {
            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "iOS BGTask: Error cancelling scheduler refresh task on expiration. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
            }
        };

        _ = Task.Run(async () =>
        {
            var success = false;
            try
            {
                MauiAppHolder.CreateAndStore();
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                await MauiProgram.WaitForBootstrapAsync();

                var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
                success = await schedulerService.HandleAsync();
            }
            catch (OperationCanceledException)
            {
                // Expiration or explicit cancellation; expected, don't log.
                success = false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "iOS BGTask: Error running scheduler refresh. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
                success = false;
            }
            finally
            {
                try
                {
                    task.SetTaskCompleted(success);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "iOS BGTask: Error completing scheduler refresh task. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
                }

                try
                {
                    cancellationTokenSource.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "iOS BGTask: Error disposing cancellation token source. Identifier={Identifier}", SchedulerRefreshTaskIdentifier);
                }
            }
        }, cancellationTokenSource.Token);
    }
}

