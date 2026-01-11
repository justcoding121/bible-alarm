#nullable enable
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using FluxorDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects;

/// <summary>
/// Fluxor effect that handles SetCarPlayScreenAction by fetching default schedule metadata
/// and dispatching it to state. Used by both iOS and Android for car play screen initialization.
/// Platform-specific UI updates (e.g., MediaSession on Android) are handled by platform-specific effects.
/// </summary>
public class DefaultCarScreenEffect(
    IDefaultScheduleService defaultScheduleService)
{
    private static readonly ILogger logger = Log.ForContext<DefaultCarScreenEffect>();

    [EffectMethod]
    public async Task HandleSetCarPlayScreen(SetCarPlayScreenAction action, FluxorDispatcher dispatcher)
    {
        try
        {
            logger.Debug("SetCarPlayScreenAction received - fetching default schedule metadata");

            // Fetch metadata on background thread to avoid blocking UI
            var metadata = await Task.Run(async () =>
                await defaultScheduleService.GetNextScheduleTrackMetaDataAsync());

            logger.Information("SetCarPlayScreenAction: Fetched default schedule metadata - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}",
                metadata.ScheduleId, metadata.Title, metadata.Artist);

            // Dispatch metadata to state
            dispatcher.Dispatch(new SetDefaultScheduleMetadataAction
            {
                ScheduleId = metadata.ScheduleId,
                Title = metadata.Title,
                Artist = metadata.Artist,
                Album = metadata.Album,
                ArtworkUrl = metadata.ArtworkUrl
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling SetCarPlayScreenAction");
        }
    }
}

