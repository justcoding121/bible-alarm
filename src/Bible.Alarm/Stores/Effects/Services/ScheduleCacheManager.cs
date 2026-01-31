#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles cache operations for schedules.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class ScheduleCacheManager
{
    private readonly IDiskCacheService? diskCacheService;
    private readonly Fluxor.IDispatcher? dispatcher;
    private const string CacheKey = "ScheduleList";

    public ScheduleCacheManager(IDiskCacheService? diskCacheService = null, Fluxor.IDispatcher? dispatcher = null)
    {
        this.diskCacheService = diskCacheService ?? ServiceProviderManager.GetService<IDiskCacheService>();
        this.dispatcher = dispatcher ?? ServiceProviderManager.GetService<Fluxor.IDispatcher>();
    }

    /// <summary>
    /// Clears the schedule list cache before operations that will modify schedules.
    /// This prevents stale cache if the process crashes after save but before refresh.
    /// </summary>
    public void InvalidateScheduleCache()
    {
        if (diskCacheService == null)
        {
            return;
        }

        try
        {
            Log.Debug("ScheduleEffects: Invalidating schedule cache");
            diskCacheService.Remove(CacheKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error invalidating schedule cache");
        }
    }

    /// <summary>
    /// Refreshes a single schedule in cache and dispatches UpdateScheduleSuccessAction with populated display names.
    /// More efficient than refreshing the entire list when only one schedule changed.
    /// </summary>
    public async Task RefreshSingleScheduleAsync(int scheduleId)
    {
        if (diskCacheService == null || dispatcher == null)
        {
            return;
        }

        try
        {
            var services = CommonBootstrapHelper.GetRequiredServicesForCache();
            if (services == null)
            {
                Log.Warning("ScheduleEffects: Cannot refresh schedule - required services not available");
                return;
            }

            // Load the single schedule from database
            var alarmSchedule = await services.AlarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: true,
                includeBiblePublication: true,
                CancellationToken.None);

            if (alarmSchedule == null)
            {
                Log.Warning("ScheduleEffects: Schedule {ScheduleId} not found in database", scheduleId);
                return;
            }

            // Map to ScheduleStateItem
            var scheduleStateItem = services.Mapper.Map<ScheduleStateItem>(alarmSchedule);

            // Populate display names
            var scheduleDisplayNameService = ServiceProviderManager.GetService<IScheduleDisplayNameService>();
            if (scheduleDisplayNameService != null)
            {
                await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, alarmSchedule);
            }

            // Update cache - refresh entire cache to keep it in sync
            var languagesDict = await CommonBootstrapHelper.LoadLanguagesDictionaryForCache(services.BiblePublicationService);
            var schedulesList = await CommonBootstrapHelper.LoadSchedulesListAsyncForCache(services, languagesDict);
            await diskCacheService.SetAsync(CacheKey, schedulesList);

            // Dispatch UpdateScheduleSuccessAction with populated display names
            // Set SkipCacheRefresh=true to prevent HandleUpdateScheduleSuccess from triggering another cache refresh (which would cause a cycle)
            // This updates only the single schedule in the Fluxor state
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Log.Debug("ScheduleEffects: Dispatching UpdateScheduleSuccessAction for schedule {ScheduleId} - PublicationCode: {PublicationCode}, SectionName: '{SectionName}', TrackTitle: '{TrackTitle}', HasSectionStructure: {HasSectionStructure}",
                    scheduleId,
                    scheduleStateItem.BiblePublicationCode ?? "null",
                    scheduleStateItem.BiblePublicationSectionName ?? "null",
                    scheduleStateItem.BiblePublicationTrackTitle ?? "null",
                    PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode));
                
                dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem, skipCacheRefresh: true));
                Log.Debug("ScheduleEffects: Dispatched UpdateScheduleSuccessAction with populated display names for schedule {ScheduleId} (skipCacheRefresh=true)", scheduleId);
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error refreshing schedule {ScheduleId}", scheduleId);
        }
    }

    /// <summary>
    /// Refreshes the schedule list cache in the background after successful DB operations.
    /// Called after successful create or delete operations.
    /// Note: For update operations, use RefreshSingleScheduleAsync instead.
    /// </summary>
    public async Task RefreshScheduleCacheAsync()
    {
        if (diskCacheService == null)
        {
            return;
        }

        try
        {
            // Fast path: refresh cache directly from Fluxor state to avoid unnecessary DB queries.
            // This is safe after create/delete because reducers already updated the Schedules collection.
            // It also avoids expensive media-index lookups (publications/tracks) that the DB-based factory performs.
            var appState = ServiceProviderManager.GetService<IState<ApplicationState>>();
            var schedulesFromState = appState?.Value.Schedules;
            if (schedulesFromState != null)
            {
                // Only persist saved schedules (Id > 0). New/unsaved schedules should never be cached to disk.
                var schedulesList = schedulesFromState
                    .Where(s => s.Id > 0)
                    .Select(s => s.DeepClone())
                    .ToList();

                await diskCacheService.SetAsync(CacheKey, schedulesList);
                Log.Information("ScheduleEffects: Refreshed schedule cache from state with {Count} schedules", schedulesList.Count);
                return;
            }

            // Refresh cache by calling the factory
            // This will reload schedules from database and repopulate the cache
            var services = CommonBootstrapHelper.GetRequiredServicesForCache();
            if (services == null)
            {
                Log.Warning("ScheduleEffects: Cannot refresh cache - required services not available");
                return;
            }

            var languagesDict = await CommonBootstrapHelper.LoadLanguagesDictionaryForCache(services.BiblePublicationService);
            var schedulesList = await CommonBootstrapHelper.LoadSchedulesListAsyncForCache(services, languagesDict);

            await diskCacheService.SetAsync(CacheKey, schedulesList);
            Log.Information("ScheduleEffects: Refreshed schedule cache with {Count} schedules", schedulesList.Count);

            // Note: We don't dispatch InitializeAction here because:
            // - For create: CreateScheduleSuccessAction reducer already adds the schedule to state
            // - For delete: RemoveScheduleSuccessAction reducer already removes the schedule from state
            // - For update: Use RefreshSingleScheduleAsync which dispatches UpdateScheduleSuccessAction
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error refreshing schedule cache");
        }
    }
}

