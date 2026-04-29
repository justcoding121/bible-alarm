#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
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
    private const string ErrorMessagePlaybackRetry = "Playback failed, check your connection then tap Retry";
    private const string ErrorMessageDownloadFailedCheckInternet = "Download failed, check your internet connection";
    private const string ErrorMessageMediaDownloadFailedCheckInternet = "Media download failed, check your internet connection";

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
                ErrorMessage = "Media playback failed. Playing fallback alarm sound."
            });

            // Show alarm notification and try to play fallback sound
            try
            {
                await notificationService.ShowNotificationAsync(currentScheduleId.Value);
                await PlayFallbackAlarmSoundAsync(setPlaylist, setCurrentTrackIndex, playCurrentTrackAsync);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing alarm notification or playing fallback alarm sound");
                // Update error message if fallback also fails
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Media playback failed. Check your internet connection."
                });
            }
        }
        else
        {
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = ErrorMessagePlaybackRetry
            });

            WeakReferenceMessenger.Default.Send(new ShowToastMessage(ErrorMessagePlaybackRetry));
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
                logger.Warning("Failed to get fallback alarm track");
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = ErrorMessageDownloadFailedCheckInternet
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
            logger.Error(ex, "Error playing fallback alarm sound");
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = ErrorMessageDownloadFailedCheckInternet
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
                logger.Error("Failed to get fallback alarm track");
                // Even for alarms, if fallback fails, show error but keep modal open
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = ErrorMessageMediaDownloadFailedCheckInternet
                });
                WeakReferenceMessenger.Default.Send(new ShowToastMessage(ErrorMessageMediaDownloadFailedCheckInternet));
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
            logger.Error(ex, "Error playing fallback alarm sound");
            // Even for alarms, if fallback fails, show error but keep modal open
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = ErrorMessageMediaDownloadFailedCheckInternet
            });
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(ErrorMessageMediaDownloadFailedCheckInternet));
            // Don't reset - keep modal open so user can see the error
        }
    }
}

