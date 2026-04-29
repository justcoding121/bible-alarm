#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles playback failure scenarios and fallback logic.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackFailureHandler
{
    private readonly IFallbackAlarmSoundService fallbackAlarmSoundService;
    private readonly INotificationService notificationService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaybackFailureHandler(
        IFallbackAlarmSoundService fallbackAlarmSoundService,
        INotificationService notificationService,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.fallbackAlarmSoundService = fallbackAlarmSoundService;
        this.notificationService = notificationService;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public async Task HandlePlaybackFailureAsync(PlaybackHandleFailureRequest request)
    {
        var isAlarm = request.IsAlarm;
        var currentScheduleId = request.CurrentScheduleId;
        var resetAsync = request.ResetAsync;
        var setPlaylist = request.SetPlaylist;
        var setCurrentTrackIndex = request.SetCurrentTrackIndex;
        var playCurrentTrackAsync = request.PlayCurrentTrackAsync;

        // Reset player and playlist service
        await resetAsync();

        if (isAlarm && currentScheduleId.HasValue)
        {
            // For alarms, dispatch error message so alarm modal shows the error
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = PlaybackUserFacingStrings.MediaPlaybackFailedPlayingFallbackAlarm
            });

            // Show alarm notification and try to play fallback sound
            try
            {
                await notificationService.ShowNotificationAsync(currentScheduleId.Value);
                await PlayFallbackAlarmSoundAsync(setPlaylist, setCurrentTrackIndex, playCurrentTrackAsync);
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.PlaybackFailureHandlerDiagnosticsLog.ErrorShowingAlarmNotificationOrPlayingFallback);
                // Update error message if fallback also fails
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = PlaybackUserFacingStrings.MediaPlaybackFailedCheckInternet
                });
            }
        }
        else
        {
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = PlaybackUserFacingStrings.PlaybackRetryConnectionThenTapRetry
            });

            WeakReferenceMessenger.Default.Send(new ShowToastMessage(PlaybackUserFacingStrings.PlaybackRetryConnectionThenTapRetry));
        }
    }

    public async Task TryPlayFallbackWhenPrepareFailedAsync(PlaybackPrepareFallbackRequest request)
    {
        var scheduleId = request.ScheduleId;
        var keepErrorMessage = request.KeepErrorMessage;
        var setPlaylist = request.SetPlaylist;
        var setCurrentTrackIndex = request.SetCurrentTrackIndex;
        var clearManuallyVisited = request.ClearManuallyVisited;
        var notifyNavigationChanged = request.NotifyNavigationChanged;
        var playCurrentTrackAsync = request.PlayCurrentTrackAsync;

        try
        {
            await notificationService.ShowNotificationAsync(scheduleId);

            var fallbackTrack = await fallbackAlarmSoundService.GetFallbackAlarmTrackAsync();
            if (fallbackTrack is null)
            {
                logger.Warning(AppConstants.Logging.PlaybackFailureHandlerDiagnosticsLog.FailedToGetFallbackAlarmTrack);
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = PlaybackUserFacingStrings.DownloadFailedCheckInternet
                });
                return;
            }

            if (!keepErrorMessage)
            {
                dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });
            }

            var playlist = new List<AudioPlayerTrack> { fallbackTrack };
            setPlaylist(playlist);
            setCurrentTrackIndex(0);
            clearManuallyVisited();
            notifyNavigationChanged(playlist, 0);
            await playCurrentTrackAsync(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackFailureHandlerDiagnosticsLog.ErrorPlayingFallbackAlarmSound);
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = PlaybackUserFacingStrings.DownloadFailedCheckInternet
            });
        }
    }

    private async Task PlayFallbackAlarmSoundAsync(
        Action<List<AudioPlayerTrack>> setPlaylist,
        Action<int> setCurrentTrackIndex,
        Func<bool, Task> playCurrentTrackAsync)
    {
        try
        {
            var fallbackTrack = await fallbackAlarmSoundService.GetFallbackAlarmTrackAsync();
            if (fallbackTrack is null)
            {
                logger.Error(AppConstants.Logging.PlaybackFailureHandlerDiagnosticsLog.FailedToGetFallbackAlarmTrack);
                // Even for alarms, if fallback fails, show error but keep modal open
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet
                });
                WeakReferenceMessenger.Default.Send(new ShowToastMessage(PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet));
                // Don't reset - keep modal open so user can see the error
                return;
            }

            // Clear any previous error when starting fallback playback
            dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });

            setPlaylist([fallbackTrack]);
            setCurrentTrackIndex(0);
            await playCurrentTrackAsync(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackFailureHandlerDiagnosticsLog.ErrorPlayingFallbackAlarmSound);
            // Even for alarms, if fallback fails, show error but keep modal open
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet
            });
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet));
            // Don't reset - keep modal open so user can see the error
        }
    }
}

