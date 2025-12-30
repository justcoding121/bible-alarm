#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.UI;

public sealed class AlarmModalService(
    ILogger logger,
    INavigationService navigationService,
    IState<PlaybackState> playbackState,
    IScheduleItemStateService scheduleItemStateService,
    IDispatcher dispatcher)
    : IAlarmModalService
{
    private readonly IDispatcher dispatcher = dispatcher;
    private bool isModalOpen;
    private bool isDisposed;

    public void SubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        playbackState.StateChanged += OnPlaybackStateChanged;
    }

    public void UnsubscribeToPlaybackStateChanges()
    {
        isModalOpen = false;
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }


    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        var shouldShowModal = playbackState.Value.IsPreparingOrPlaying;

        _ = shouldShowModal switch
        {
            true when !isModalOpen => MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    logger.Information("PlaybackState changed - showing AlarmModal (IsPreparingOrPlaying: true)");

                    // Hide Home page before opening modal to prevent visual flash
                    navigationService.SetHomePageVisibility(isPlaybackActive: true);

                    await navigationService.OpenAlarmModalAsync();
                    isModalOpen = true;
                    logger.Information("AlarmModal opened");

                    // Set IsBusy to false for the schedule item after modal is shown
                    scheduleItemStateService.SetScheduleItemBusyToFalse(playbackState.Value.CurrentScheduleId);
                    // Note: Home page overlay will be hidden when Alarm Modal Appearing event fires
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error showing AlarmModal");
                }
            }),
            false when isModalOpen => MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    logger.Information("PlaybackState changed - hiding AlarmModal (IsPreparingOrPlaying: false)");

                    // Show Home page before closing modal
                    navigationService.SetHomePageVisibility(isPlaybackActive: false);

                    await navigationService.PopModalAsync();
                    isModalOpen = false;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error hiding AlarmModal");
                }
            }),
            _ => Task.CompletedTask
        };
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unsubscribe from playback state changes
        playbackState.StateChanged -= OnPlaybackStateChanged;
    }
}

