#nullable enable
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles command initialization for AlarmViewModal.
/// Separated from AlarmViewModal for better modularity.
/// </summary>
public class AlarmViewModelCommandInitializer
{
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly Func<Task> handleReviewRequestAsync;
    private readonly Action beginStoppingUi;

    public AlarmViewModelCommandInitializer(
        ILogger logger,
        IPlaybackService playbackService,
        Func<Task> handleReviewRequestAsync,
        Action beginStoppingUi)
    {
        this.logger = logger;
        this.playbackService = playbackService;
        this.handleReviewRequestAsync = handleReviewRequestAsync;
        this.beginStoppingUi = beginStoppingUi;
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

    public ICommand CreateCancelCommand()
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

    public ICommand CreatePreviousCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            await playbackService.PlayPreviousAsync();
        });
    }

    public ICommand CreateNextCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
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
        });
    }

    public ICommand CreateRetryCommand(Func<int?> getCurrentScheduleId, Func<bool> hasError)
    {
        return new AsyncRelayCommand(async () =>
        {
            var scheduleId = getCurrentScheduleId();
            if (scheduleId.HasValue)
            {
                logger.Information("RetryCommand executed - retrying playback for schedule {ScheduleId}", scheduleId.Value);
                try
                {
                    // Save schedule ID before reset
                    var scheduleIdToRetry = scheduleId.Value;
                    // Note: ResetAndRetryAsync will preserve the original isAlarm value from state
                    // The modal will stay open and update automatically as state changes

                    // Reset internally without closing modal - just reset player and state
                    // Then immediately prepare and play again - modal will update automatically
                    await playbackService.ResetAndRetryAsync(scheduleIdToRetry);
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
        }, () => hasError() && getCurrentScheduleId().HasValue);
    }
}

