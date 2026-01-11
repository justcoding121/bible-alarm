#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles cache operations for schedules.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class ScheduleCacheManager
{
    private readonly IDiskCacheService? diskCacheService;
    private const string CacheKey = "ScheduleList";

    public ScheduleCacheManager(IDiskCacheService? diskCacheService = null)
    {
        this.diskCacheService = diskCacheService ?? ServiceProviderManager.GetService<IDiskCacheService>();
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
    /// Refreshes the schedule list cache in the background after successful DB operations.
    /// Called after successful create, update, or delete operations.
    /// </summary>
    public async Task RefreshScheduleCacheAsync()
    {
        if (diskCacheService == null)
        {
            return;
        }

        try
        {
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
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error refreshing schedule cache");
        }
    }
}

