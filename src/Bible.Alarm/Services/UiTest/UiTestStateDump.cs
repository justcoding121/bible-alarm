#if UITEST
#nullable enable

using System.Text.Json;
using Bible.Alarm.Common;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.UiTest;

/// <summary>
/// One-line JSON snapshot of playback and schedule ids for UI tests. Compiled only when UiTestHooks is set.
/// </summary>
public static class UiTestStateDump
{
    public const string LogTag = "BibleAlarmUiTest";

    private static readonly ILogger logger = Log.ForContext(typeof(UiTestStateDump));

    public static string ToJson()
    {
        try
        {
            if (!MauiAppHolder.IsInitialized)
            {
                return JsonSerializer.Serialize(new { ok = false, error = "app-not-initialized" });
            }

            var playback = ServiceProviderManager.GetService<IState<PlaybackState>>();
            var application = ServiceProviderManager.GetService<IState<ApplicationState>>();
            var scheduleIds = application?.Value?.Schedules?.Select(item => item.Id).ToArray() ?? [];
            var payload = new
            {
                ok = true,
                status = playback?.Value?.Status.ToString() ?? "Unknown",
                scheduleId = playback?.Value?.CurrentScheduleId,
                title = playback?.Value?.Title,
                preparingOrPlaying = playback?.Value?.IsPreparingOrPlaying ?? false,
                scheduleIds
            };
            var json = JsonSerializer.Serialize(payload);
            logger.Information("{Tag} {Json}", LogTag, json);
            return json;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{Tag} dump failed", LogTag);
            return JsonSerializer.Serialize(new { ok = false, error = ex.GetType().Name });
        }
    }
}
#endif
