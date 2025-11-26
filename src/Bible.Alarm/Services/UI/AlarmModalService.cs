#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class AlarmModalService
{
    private readonly ILogger _logger;
    private readonly INavigationService _navigationService;
    private readonly IState<PlaybackState> _playbackState;
    private readonly ScheduleItemStateService _scheduleItemStateService;
    private bool _isModalOpen;

    public AlarmModalService(
        ILogger logger,
        INavigationService navigationService,
        IState<PlaybackState> playbackState,
        ScheduleItemStateService scheduleItemStateService)
    {
        _logger = logger;
        _navigationService = navigationService;
        _playbackState = playbackState;
        _scheduleItemStateService = scheduleItemStateService;
    }

    public void SubscribeToPlaybackStateChanges()
    {
        _playbackState.StateChanged += OnPlaybackStateChanged;
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        var shouldShowModal = _playbackState.Value.IsPreparingOrPlaying;
        
        if (shouldShowModal && !_isModalOpen)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    _logger.Information("PlaybackState changed - showing AlarmModal (IsPreparingOrPlaying: true)");
                    await _navigationService.OpenAlarmModalAsync();
                    _isModalOpen = true;
                    _logger.Information("AlarmModal opened");
                    
                    // Set IsBusy to false for the schedule item after modal is shown
                    _scheduleItemStateService.SetScheduleItemBusyToFalse(_playbackState.Value.CurrentScheduleId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error showing AlarmModal");
                }
            });
        }
        else if (!shouldShowModal && _isModalOpen)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    _logger.Information("PlaybackState changed - hiding AlarmModal (IsPreparingOrPlaying: false)");
                    await _navigationService.PopModalAsync();
                    _isModalOpen = false;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error hiding AlarmModal");
                }
            });
        }
    }
}

