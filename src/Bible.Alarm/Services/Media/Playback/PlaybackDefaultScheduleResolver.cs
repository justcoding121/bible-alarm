#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Resolves the default schedule ID when play button is pressed with no active playback.
/// Fluxor state may not have DefaultScheduleId yet during cold start, so falls back to Preferences.
/// </summary>
public static class PlaybackDefaultScheduleResolver
{
    public static int? Resolve(IState<PlaybackState> playbackState, ILogger logger)
    {
        var defaultScheduleId = playbackState.Value.DefaultScheduleId;

        if (defaultScheduleId.HasValue && defaultScheduleId.Value > 0)
        {
            return defaultScheduleId;
        }

        var lastPlayed = LastPlayedMetadataHelper.GetLastPlayedMetadata();
        if (lastPlayed?.ScheduleId is > 0)
        {
            logger.Information("DefaultScheduleId not in Fluxor state, using Preferences fallback: {ScheduleId}", lastPlayed.Value.ScheduleId);
            return lastPlayed.Value.ScheduleId;
        }

        return null;
    }
}
