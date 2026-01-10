#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Serilog;
#if ANDROID
using Bible.Alarm.Platforms.Android.Effects;
#endif

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Service for loading schedules and populating state during bootstrap.
/// </summary>
public class ScheduleBootstrapService : IScheduleBootstrapService
{
    private readonly IDatabaseSeedService databaseSeedService;
    private readonly IScheduleMigrationService scheduleMigrationService;
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IDispatcher dispatcher;
    private readonly IBiblePublicationService? BiblePublicationService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly IMapper mapper;
    private readonly IMediaService? mediaService;
    private readonly IMelodyMusicService? melodyMusicService;
    private readonly IDiskCacheService? diskCacheService;
    private readonly ScheduleStatePopulator statePopulator;

    public ScheduleBootstrapService(
        IDatabaseSeedService databaseSeedService,
        IScheduleMigrationService scheduleMigrationService,
        IAlarmScheduleService alarmScheduleService,
        IDispatcher dispatcher,
        IBiblePublicationService? BiblePublicationService,
        IBiblePublicationSectionService? biblePublicationSectionService,
        IMapper mapper,
        IMediaService? mediaService,
        IMelodyMusicService? melodyMusicService,
        IDiskCacheService? diskCacheService)
    {
        this.databaseSeedService = databaseSeedService;
        this.scheduleMigrationService = scheduleMigrationService;
        this.alarmScheduleService = alarmScheduleService;
        this.dispatcher = dispatcher;
        this.BiblePublicationService = BiblePublicationService;
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.mapper = mapper;
        this.mediaService = mediaService;
        this.melodyMusicService = melodyMusicService;
        this.diskCacheService = diskCacheService;
        this.statePopulator = new ScheduleStatePopulator(
            BiblePublicationService,
            biblePublicationSectionService,
            mapper,
            mediaService,
            melodyMusicService);
    }

    public async Task<bool> SeedAndMigrateAsync()
    {
        bool scheduleWasSeeded = false;
        try
        {
            // Seed default schedule if database is empty
            // Schema is guaranteed to exist at this point (verified in InitializeDatabase)
            scheduleWasSeeded = await databaseSeedService.SeedDefaultAlarmAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't fail bootstrap - user can still use the app
            // If seeding fails, the database might be corrupted or schema issue
            Log.Logger.Error(ex, "[BOOTSTRAP] Failed to seed default schedule, continuing bootstrap");
        }

        try
        {
            // Migrate Bible Gateway schedules (legacy migration)
            await scheduleMigrationService.MigrateBibleGatewaySchedulesAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't fail bootstrap
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to migrate Bible Gateway schedules, continuing bootstrap");
        }

        return scheduleWasSeeded;
    }

    public async Task InitializeAsync()
    {
#if DEBUG
        var schedulesStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Log.Logger.Information("[BOOTSTRAP] Schedule initialization starting");
#endif

        try
        {
            // Parallelize seed/migration with language loading
            // Languages can load independently while we seed/migrate schedules
#if DEBUG
            var seedStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
            var languagesStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            var seedTask = SeedAndMigrateAsync();
            var languagesTask = LoadLanguagesDictionaryAsync();

            // Wait for seed to complete before loading schedules (schedules may be created during seed)
            var scheduleWasSeeded = await seedTask;
#if DEBUG
            var seedElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - seedStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Schedule seed/migration completed in {ElapsedMs:F2}ms", seedElapsed);
#endif

            // Load schedules from cache or factory
            // Cache stores as List<ScheduleStateItem> for JSON serialization
            const string CacheKey = "ScheduleList";

#if DEBUG
            var cacheStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ObservableHashSet<ScheduleStateItem> initialSchedules;
            Dictionary<string, Language>? languagesDict = null;

            if (diskCacheService != null)
            {
                // Languages task is already running in parallel, await it now for the factory
                languagesDict = await languagesTask;
#if DEBUG
                var languagesElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - languagesStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Logger.Information("[BOOTSTRAP] Loaded languages dictionary in {ElapsedMs:F2}ms", languagesElapsed);
#endif

                // If a schedule was seeded, invalidate cache to ensure the new schedule is included
                if (scheduleWasSeeded)
                {
                    Log.Logger.Debug("[BOOTSTRAP] Schedule was seeded, invalidating cache to include new schedule");
                    diskCacheService.Remove(CacheKey);
                }

                // Use cache with factory - factory will be called if cache miss or deserialization fails
                var cachedSchedulesList = await diskCacheService.GetOrSetAsync(
                    CacheKey,
                    async () =>
                    {
                        // Factory: Load schedules from database and populate state items
                        return await LoadSchedulesListAsync(languagesDict);
                    });

                // Convert List to ObservableHashSet
                initialSchedules = new ObservableHashSet<ScheduleStateItem>();
                foreach (var item in cachedSchedulesList)
                {
                    initialSchedules.Add(item);
                }

#if DEBUG
                var cacheElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - cacheStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Logger.Information("[BOOTSTRAP] Loaded {Count} schedules from cache in {ElapsedMs:F2}ms", initialSchedules.Count, cacheElapsed);
#endif
            }
            else
            {
                // Fallback if cache service not available
                Log.Logger.Warning("[BOOTSTRAP] IDiskCacheService not available, loading schedules without cache");
                languagesDict = await languagesTask;
#if DEBUG
                var languagesElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - languagesStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Logger.Information("[BOOTSTRAP] Loaded languages dictionary in {ElapsedMs:F2}ms", languagesElapsed);
#endif
                var schedulesList = await LoadSchedulesListAsync(languagesDict);
                initialSchedules = new ObservableHashSet<ScheduleStateItem>();
                foreach (var item in schedulesList)
                {
                    initialSchedules.Add(item);
                }
            }

#if DEBUG
            var dispatchStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            await DispatchInitializeActionAsync(initialSchedules);
#if DEBUG
            var dispatchElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dispatchStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Dispatched InitializeAction in {ElapsedMs:F2}ms", dispatchElapsed);
#endif

            // After initial load from cache, invalidate and refresh cache in background (non-blocking)
            // This ensures cache corruption is detected and fixed automatically.
            // UI is only updated if differences are detected between cached and fresh data.
            if (diskCacheService != null)
            {
                // Capture languagesDict for background refresh
                var languagesDictForRefresh = languagesDict;
                // Fire and forget - runs asynchronously without blocking UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefreshCacheAndUpdateStateIfNeededAsync(initialSchedules, languagesDictForRefresh);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "[BOOTSTRAP] Error refreshing cache after initial load");
                    }
                });
            }

#if DEBUG
            var schedulesElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - schedulesStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Schedule initialization completed in {ElapsedMs:F2}ms", schedulesElapsed);
#endif

#if ANDROID || IOS
            // Dispatch SetCarPlayScreenAction to fetch and set default schedule metadata for car displays
            // Android: Updates MediaSession for Android Auto
            // iOS: Updates MPNowPlayingInfoCenter for CarPlay and Lock Screen
            dispatcher.Dispatch(new SetCarPlayScreenAction());
            Log.Logger.Debug("SetCarPlayScreenAction dispatched after bootstrap completion");
#endif
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error initializing schedules in bootstrap");
        }
    }

    public async Task<Dictionary<string, Language>?> LoadLanguagesDictionaryAsync()
    {
        if (BiblePublicationService == null)
        {
            return null;
        }

        try
        {
            var languagesDict = await BiblePublicationService.GetDistinctLanguagesAsync();
            Log.Logger.Information("Loaded {Count} languages for translation name lookup", languagesDict.Count);
            return languagesDict;
        }
        catch (Exception langEx)
        {
            Log.Logger.Warning(langEx, "Error loading languages - translation names will not be populated");
            return null;
        }
    }

    public async Task<List<ScheduleStateItem>> LoadSchedulesListAsync(Dictionary<string, Language>? languagesDict)
    {
#if DEBUG
        var loadStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var alarmSchedules = await LoadSchedulesFromDatabaseAsync();
#if DEBUG
        var loadElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - loadStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Loaded {Count} schedules from database in {ElapsedMs:F2}ms", alarmSchedules.Count, loadElapsed);
#endif

#if DEBUG
        var populateStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var scheduleStateItems = await statePopulator.PopulateAsync(alarmSchedules, languagesDict);
#if DEBUG
        var populateElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - populateStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Populated {Count} schedule state items in {ElapsedMs:F2}ms", scheduleStateItems.Count, populateElapsed);
#endif

        // Convert ObservableHashSet to List for JSON serialization
        return scheduleStateItems.ToList();
    }

    private async Task<List<AlarmSchedule>> LoadSchedulesFromDatabaseAsync()
    {
        var alarmSchedules = await alarmScheduleService.GetAllSchedulesAsync(
            includeMusic: true,
            includeBiblePublication: true);

        Log.Logger.Information("Loaded {Count} schedules from database during bootstrap", alarmSchedules.Count);
        return alarmSchedules;
    }

    private async Task DispatchInitializeActionAsync(ObservableHashSet<ScheduleStateItem> initialSchedules)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    dispatcher.Dispatch(new InitializeAction(initialSchedules));
                    Log.Logger.Information("Dispatched InitializeAction with {Count} schedules", initialSchedules.Count);
                }
                catch (Exception dispatchEx)
                {
                    Log.Logger.Error(dispatchEx, "Error dispatching InitializeAction");
                    throw;
                }
            });
        }
        catch (Exception mainThreadEx)
        {
            Log.Logger.Error(mainThreadEx, "Error invoking MainThread for InitializeAction dispatch");
            throw;
        }
    }

    /// <summary>
    /// Refreshes the cache from database and updates state ONLY if data differs from cached version.
    /// Runs asynchronously in background without blocking UI.
    /// This ensures cache corruption is detected and fixed automatically.
    /// </summary>
    private async Task RefreshCacheAndUpdateStateIfNeededAsync(
        ObservableHashSet<ScheduleStateItem> cachedSchedules,
        Dictionary<string, Language>? languagesDict)
    {
        const string CacheKey = "ScheduleList";

        try
        {
            Log.Logger.Debug("[BOOTSTRAP] Starting cache refresh after initial load (background, non-blocking)");

            // Invalidate cache to force fresh load
            diskCacheService?.Remove(CacheKey);

            // Load fresh data from database
            var freshSchedulesList = await LoadSchedulesListAsync(languagesDict);

            // Convert to ObservableHashSet for comparison
            var freshSchedules = new ObservableHashSet<ScheduleStateItem>();
            foreach (var item in freshSchedulesList)
            {
                freshSchedules.Add(item);
            }

            // Compare cached vs fresh data
            if (AreSchedulesDifferent(cachedSchedules, freshSchedules))
            {
                Log.Logger.Information(
                    "[BOOTSTRAP] Cache refresh detected differences - cached: {CachedCount}, fresh: {FreshCount}. Updating UI state with fresh data",
                    cachedSchedules.Count, freshSchedules.Count);

                // Update cache with fresh data
                if (diskCacheService != null)
                {
                    await diskCacheService.SetAsync(CacheKey, freshSchedulesList);
                    Log.Logger.Debug("[BOOTSTRAP] Cache repopulated with fresh data");
                }

                // Update UI state with fresh data (only if differences detected)
                await DispatchInitializeActionAsync(freshSchedules);
                Log.Logger.Information("[BOOTSTRAP] UI state updated with refreshed data");
            }
            else
            {
                Log.Logger.Debug("[BOOTSTRAP] Cache refresh - no differences detected, repopulating cache without UI update");
                
                // Repopulate cache even if no differences (ensures cache is valid)
                // UI is NOT updated since data is identical
                if (diskCacheService != null)
                {
                    await diskCacheService.SetAsync(CacheKey, freshSchedulesList);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "[BOOTSTRAP] Error during cache refresh");
            throw;
        }
    }

    /// <summary>
    /// Compares two schedule collections to detect differences.
    /// Returns true if schedules differ in count, IDs, or key properties.
    /// </summary>
    private static bool AreSchedulesDifferent(
        ObservableHashSet<ScheduleStateItem> cached,
        ObservableHashSet<ScheduleStateItem> fresh)
    {
        // Quick check: different count
        if (cached.Count != fresh.Count)
        {
            return true;
        }

        // Create lookup by ID for efficient comparison
        var cachedById = cached.ToDictionary(s => s.Id);
        var freshById = fresh.ToDictionary(s => s.Id);

        // Check if all IDs match
        if (cachedById.Keys.Count != freshById.Keys.Count ||
            !cachedById.Keys.All(id => freshById.ContainsKey(id)) ||
            !freshById.Keys.All(id => cachedById.ContainsKey(id)))
        {
            return true;
        }

        // Compare key properties for each schedule
        foreach (var cachedSchedule in cached)
        {
            if (!freshById.TryGetValue(cachedSchedule.Id, out var freshSchedule))
            {
                return true;
            }

            // Compare key properties that matter for state
            if (cachedSchedule.Name != freshSchedule.Name ||
                cachedSchedule.IsEnabled != freshSchedule.IsEnabled ||
                cachedSchedule.Hour != freshSchedule.Hour ||
                cachedSchedule.Minute != freshSchedule.Minute ||
                cachedSchedule.Second != freshSchedule.Second ||
                cachedSchedule.DaysOfWeek != freshSchedule.DaysOfWeek ||
                cachedSchedule.NotificationEnabled != freshSchedule.NotificationEnabled ||
                cachedSchedule.MusicEnabled != freshSchedule.MusicEnabled ||
                cachedSchedule.SnoozeMinutes != freshSchedule.SnoozeMinutes ||
                cachedSchedule.NumberOfTracksToRead != freshSchedule.NumberOfTracksToRead ||
                cachedSchedule.AlwaysPlayFromStart != freshSchedule.AlwaysPlayFromStart ||
                cachedSchedule.CurrentPlayItem != freshSchedule.CurrentPlayItem ||
                cachedSchedule.BiblePublicationScheduleId != freshSchedule.BiblePublicationScheduleId ||
                cachedSchedule.BiblePublicationLanguageCode != freshSchedule.BiblePublicationLanguageCode ||
                cachedSchedule.BiblePublicationCode != freshSchedule.BiblePublicationCode ||
                cachedSchedule.BiblePublicationSectionNumber != freshSchedule.BiblePublicationSectionNumber ||
                cachedSchedule.BiblePublicationTrackNumber != freshSchedule.BiblePublicationTrackNumber ||
                cachedSchedule.MusicId != freshSchedule.MusicId ||
                cachedSchedule.MusicType != freshSchedule.MusicType ||
                cachedSchedule.MusicPublicationCode != freshSchedule.MusicPublicationCode ||
                cachedSchedule.MusicLanguageCode != freshSchedule.MusicLanguageCode ||
                cachedSchedule.MusicTrackNumber != freshSchedule.MusicTrackNumber ||
                cachedSchedule.MusicRepeat != freshSchedule.MusicRepeat)
            {
                return true;
            }
        }

        return false;
    }
}

