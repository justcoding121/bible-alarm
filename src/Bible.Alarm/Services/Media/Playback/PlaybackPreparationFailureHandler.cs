#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles dispatch, toast, and optional fallback when playlist preparation fails (null or empty).
/// </summary>
public static class PlaybackPreparationFailureHandler
{
    public static async Task HandleAsync(
        int scheduleId,
        bool isAlarm,
        string logMessage,
        IDispatcher dispatcher,
        ILogger logger,
        Func<int, bool, Task>? tryPlayFallbackAlarmSoundAsync)
    {
        logger.Information(logMessage, scheduleId);

        var errorMessage = isAlarm
            ? "Download failed. Playing default alarm sound."
            : "Media download failed. Check your internet connection.";

        dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = errorMessage });
        dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(errorMessage));

        if (isAlarm && tryPlayFallbackAlarmSoundAsync != null)
        {
            await tryPlayFallbackAlarmSoundAsync(scheduleId, true);
        }
    }
}
