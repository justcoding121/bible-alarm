#nullable enable
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

public class AlarmViewModelCommandInitializer
{
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly ISchedulePlaybackService schedulePlaybackService;
    private readonly Func<Task> handleReviewRequestAsync;
    private readonly Action beginStoppingUi;
    private readonly Action resetProgressUi;

    public AlarmViewModelCommandInitializer(
        ILogger logger,
        IPlaybackService playbackService,
        ISchedulePlaybackService schedulePlaybackService,
        Func<Task> handleReviewRequestAsync,
        Action beginStoppingUi,
        Action resetProgressUi)
    {
        this.logger = logger;
        this.playbackService = playbackService;
        this.schedulePlaybackService = schedulePlaybackService;
        this.handleReviewRequestAsync = handleReviewRequestAsync;
        this.beginStoppingUi = beginStoppingUi;
        this.resetProgressUi = resetProgressUi;
    }

    public ICommand CreateDismissCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            logger.Information("DismissCommand executed - stopping playback and cancelling downloads");
            try
            {
                // Immediately switch Stop icon -> spinner and disable controls.
                beginStoppingUi();

                // Allow at least one UI frame for the spinner to render before stopping playback.
                // Without this, fast stops can close the modal before the ActivityIndicator is visible.
                await Task.Delay(50);

                await playbackService.StopAsync();
                await handleReviewRequestAsync();
                logger.Information("DismissCommand completed successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in DismissCommand");
            }
        }, () => true);
    }

    public static ICommand CreateCancelCommand()
    {
        return new RelayCommand(() =>
        {
            // This command doesn't need navigation - the modal will be closed when playback is dismissed
        });
    }

    public ICommand CreatePlayCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await playbackService.PlayAsync();
        });
    }

    public ICommand CreatePauseCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await playbackService.PauseAsync();
        });
    }

    public ICommand CreatePreviousCommand(Action? onBeginTrackChange = null)
    {
        return new AsyncRelayCommand(async () =>
        {
            onBeginTrackChange?.Invoke();
            resetProgressUi();
            await Task.Delay(50);
            await playbackService.PlayPreviousAsync();
        });
    }

    public ICommand CreateNextCommand(Action? onBeginTrackChange = null)
    {
        return new AsyncRelayCommand(async () =>
        {
            onBeginTrackChange?.Invoke();
            resetProgressUi();
            await Task.Delay(50);
            await playbackService.PlayNextAsync();
        });
    }

    public ICommand CreateForwardCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await playbackService.SeekForwardAsync();
        });
    }

    public ICommand CreateBackwardCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await playbackService.SeekBackwardAsync();
        });
    }

    public ICommand CreateSeekCommand()
    {
        return new AsyncRelayCommand<TimeSpan>(async position =>
        {
            await playbackService.SeekToAsync(position);
        }, AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }

    public ICommand CreateRetryCommand(Func<int?> getCurrentScheduleId, Func<bool> hasError, Action<bool> setIsRetryBusy)
    {
        return new AsyncRelayCommand(async () =>
        {
            // Show spinner immediately to indicate tap was received
            setIsRetryBusy(true);
            await Task.Delay(50);

            var scheduleId = getCurrentScheduleId();
            if (scheduleId.HasValue)
            {
                logger.Information("RetryCommand executed - retrying playback for schedule {ScheduleId}", scheduleId.Value);
                try
                {
                    // Save schedule ID before reset
                    var scheduleIdToRetry = scheduleId.Value;
                    if (playbackService.IsAlarmPlaybackSession)
                    {
                        await schedulePlaybackService.PlayScheduleAsync(scheduleIdToRetry);
                    }
                    else
                    {
                        await playbackService.ResetAndRetryAsync(scheduleIdToRetry);
                    }
                    logger.Information("RetryCommand completed successfully");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in RetryCommand");
                }
            }
            else
            {
                logger.Warning("RetryCommand executed but no current schedule ID available");
            }

            setIsRetryBusy(false);
        }, () => hasError() && getCurrentScheduleId().HasValue);
    }
}

