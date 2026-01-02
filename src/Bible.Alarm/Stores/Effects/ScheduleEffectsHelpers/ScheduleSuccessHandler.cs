#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles success action effects for schedule operations.
/// </summary>
public class ScheduleSuccessHandler
{
    private readonly ScheduleCacheManager cacheManager;

    public ScheduleSuccessHandler(ScheduleCacheManager cacheManager)
    {
        this.cacheManager = cacheManager;
    }

    public Task HandleUpdateScheduleSuccess(UpdateScheduleSuccessAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug("ScheduleEffects: HandleUpdateScheduleSuccess - Schedule updated in DB, refreshing cache for schedule {ScheduleId}", action.Schedule?.Id);

            // Refresh cache in background after successful DB update
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());

            // Dispatch SetCarPlayScreenAction to refresh Android Auto metadata
            // This will trigger DefaultCarScreenEffect to fetch metadata and update MediaSession
            // MediaSessionEffect will check if playback is active and skip if needed
            dispatcher.Dispatch(new SetCarPlayScreenAction());

            Log.Information("ScheduleEffects: HandleUpdateScheduleSuccess - Invalidated cache and dispatched SetCarPlayScreenAction for schedule {ScheduleId}", action.Schedule?.Id);

            // Note: OnLoadChildren is already being called by Android Auto in response to NotifyChildrenChanged
            // which is triggered when the change tracker detects a change in OnUpdateScheduleFromViewModel.
            // No additional force refresh is needed.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleUpdateScheduleSuccess");
        }

        return Task.CompletedTask;
    }

    public async Task HandleRemoveScheduleSuccess(RemoveScheduleSuccessAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug("ScheduleEffects: HandleRemoveScheduleSuccess - Schedule deleted from DB, refreshing cache for schedule {ScheduleId}", action.ScheduleId);

            // Refresh cache in background after successful DB delete
            // Note: Cache was already cleared before delete, this just refreshes it
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());

            // Check if the deleted schedule was the last played item saved in Preferences
            var lastPlayedMetadata = LastPlayedMetadataHelper.GetLastPlayedMetadata();
            if (lastPlayedMetadata.HasValue && lastPlayedMetadata.Value.ScheduleId == action.ScheduleId)
            {
                Log.Information("ScheduleEffects: HandleRemoveScheduleSuccess - Deleted schedule {ScheduleId} was the last played item, refreshing metadata", action.ScheduleId);

                // Get default schedule service to refresh metadata
                var defaultScheduleService = ServiceProviderManager.GetService<IDefaultScheduleService>();
                if (defaultScheduleService != null)
                {
                    // GetNextScheduleTrackMetaDataAsync will automatically save to Preferences
                    await defaultScheduleService.GetNextScheduleTrackMetaDataAsync();
                    Log.Information("ScheduleEffects: HandleRemoveScheduleSuccess - Refreshed last played metadata after schedule deletion");
                }
                else
                {
                    Log.Warning("ScheduleEffects: HandleRemoveScheduleSuccess - IDefaultScheduleService not available, cannot refresh metadata");
                }
            }
            else
            {
                Log.Debug("ScheduleEffects: HandleRemoveScheduleSuccess - Deleted schedule {ScheduleId} was not the last played item (LastPlayedScheduleId: {LastPlayedScheduleId}), no refresh needed",
                    action.ScheduleId, lastPlayedMetadata?.ScheduleId?.ToString() ?? "null");
            }

            // Always dispatch SetCarPlayScreenAction to refresh Android Auto metadata after deletion
            // This ensures Android Auto gets updated default schedule metadata, matching the update flow
            dispatcher.Dispatch(new SetCarPlayScreenAction());

            Log.Information("ScheduleEffects: HandleRemoveScheduleSuccess - Invalidated cache and dispatched SetCarPlayScreenAction for deleted schedule {ScheduleId}", action.ScheduleId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleRemoveScheduleSuccess");
        }
    }
}

