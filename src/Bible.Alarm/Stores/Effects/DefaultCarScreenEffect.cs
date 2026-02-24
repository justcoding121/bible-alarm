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
    public Task HandleSetCarPlayScreen(SetCarPlayScreenAction action, FluxorDispatcher dispatcher)
    {
        logger.Debug("SetCarPlayScreenAction received - fetching default schedule metadata in background");

        _ = Task.Run(async () =>
        {
            try
            {
                var metadata = await defaultScheduleService.GetNextScheduleTrackMetaDataAsync();

                logger.Information("SetCarPlayScreenAction: Fetched default schedule metadata - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}",
                    metadata.ScheduleId, metadata.Title, metadata.Artist);

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
                logger.Error(ex, "Error handling SetCarPlayScreenAction (background)");
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles rotation of the default schedule shown on Android Auto (every 5 minutes when car connected and not playing).
    /// Fetches next schedule in rotation and dispatches SetDefaultScheduleMetadataAction; Android effect updates MediaSession.
    /// </summary>
    [EffectMethod]
    public Task HandleRotateDefaultSchedule(RotateDefaultScheduleAction action, FluxorDispatcher dispatcher)
    {
        logger.Debug("RotateDefaultScheduleAction received - fetching next schedule in rotation in background");

        _ = Task.Run(async () =>
        {
            try
            {
                var metadata = await defaultScheduleService.GetNextScheduleInRotationMetadataAsync();

                logger.Information("RotateDefaultScheduleAction: Fetched next schedule in rotation - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}",
                    metadata.ScheduleId, metadata.Title, metadata.Artist);

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
                logger.Error(ex, "Error handling RotateDefaultScheduleAction (background)");
            }
        });

        return Task.CompletedTask;
    }
}

