#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Actions.Schedule;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

public static class ScheduleSuccessHandler
{
    public static Task HandleUpdateScheduleSuccess(UpdateScheduleSuccessAction action, IDispatcher dispatcher)
    {
        try
        {
            var scheduleId = action.Schedule?.Id ?? 0;
            if (scheduleId <= 0)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleSuccessInvalidScheduleId);
                return Task.CompletedTask;
            }

            // ScheduleList disk caching removed. No post-success cache refresh.

            // Refresh Android Auto metadata via DefaultCarScreenEffect; MediaSessionEffect skips if playback is active.
            dispatcher.Dispatch(new SetCarPlayScreenAction());

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleSuccessDispatchedSetCarPlayScreenForSchedule, action.Schedule?.Id);

            // Note: OnLoadChildren is already being called by Android Auto in response to NotifyChildrenChanged
            // which is triggered when the change tracker detects a change in OnUpdateScheduleFromViewModel.
            // No additional force refresh is needed.
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleUpdateScheduleSuccess);
        }

        return Task.CompletedTask;
    }

    public static async Task HandleRemoveScheduleSuccess(RemoveScheduleSuccessAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessScheduleDeletedPostDeleteActions, action.ScheduleId);

            // Check if the deleted schedule was the last played item saved in Preferences
            var lastPlayedMetadata = LastPlayedMetadataHelper.GetLastPlayedMetadata();
            if (lastPlayedMetadata.HasValue && lastPlayedMetadata.Value.ScheduleId == action.ScheduleId)
            {
                Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessDeletedWasLastPlayedRefreshingMetadata, action.ScheduleId);

                var defaultScheduleService = ServiceProviderManager.GetService<IDefaultScheduleService>();
                if (defaultScheduleService != null)
                {
                    // GetNextScheduleTrackMetaDataAsync will automatically save to Preferences
                    await defaultScheduleService.GetNextScheduleTrackMetaDataAsync();
                    Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessRefreshedLastPlayedMetadataAfterDeletion);
                }
                else
                {
                    Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessDefaultScheduleServiceUnavailable);
                }
            }
            else
            {
                Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessDeletedWasNotLastPlayedNoRefreshNeeded,
                    action.ScheduleId, lastPlayedMetadata?.ScheduleId?.ToString() ?? "null");
            }

            // Always dispatch SetCarPlayScreenAction to refresh Android Auto metadata after deletion
            // This ensures Android Auto gets updated default schedule metadata, matching the update flow
            dispatcher.Dispatch(new SetCarPlayScreenAction());

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessInvalidatedCacheDispatchedSetCarPlayScreen, action.ScheduleId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleRemoveScheduleSuccess);
        }
    }
}

