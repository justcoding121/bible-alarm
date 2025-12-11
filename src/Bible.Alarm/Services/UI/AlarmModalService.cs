#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.UI;

public class AlarmModalService(
    ILogger logger,
    INavigationService navigationService,
    IState<PlaybackState> playbackState,
    IScheduleItemStateService scheduleItemStateService,
    IDispatcher dispatcher)
    : IAlarmModalService
{
    private readonly ILogger _logger = logger;
    private readonly INavigationService _navigationService = navigationService;
    private readonly IState<PlaybackState> _playbackState = playbackState;
    private readonly IScheduleItemStateService _scheduleItemStateService = scheduleItemStateService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private bool _isModalOpen;
    private bool _isDisposed;

    public void SubscribeToPlaybackStateChanges()
    {
        _isModalOpen = false;
        _playbackState.StateChanged += OnPlaybackStateChanged;
    }

    public void UnsubscribeToPlaybackStateChanges()
    {
        _isModalOpen = false;
        _playbackState.StateChanged -= OnPlaybackStateChanged;
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
                    // Note: Home page overlay will be hidden when Alarm Modal Appearing event fires
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
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Unsubscribe from playback state changes
        _playbackState.StateChanged -= OnPlaybackStateChanged;
    }
}

