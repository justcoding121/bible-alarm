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

    public AlarmViewModelCommandInitializer(
        ILogger logger,
        IPlaybackService playbackService,
        Func<Task> handleReviewRequestAsync)
    {
        this.logger = logger;
        this.playbackService = playbackService;
        this.handleReviewRequestAsync = handleReviewRequestAsync;
    }

    public ICommand CreateDismissCommand()
    {
        return new AsyncRelayCommand(async () =>
        {
            logger.Information("DismissCommand executed - stopping playback and cancelling downloads");
            try
            {
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
}

