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
    private readonly IBibleTranslationService? bibleTranslationService;
    private readonly IBibleBookService? bibleBookService;
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
        IBibleTranslationService? bibleTranslationService,
        IBibleBookService? bibleBookService,
        IMapper mapper,
        IMediaService? mediaService,
        IMelodyMusicService? melodyMusicService,
        IDiskCacheService? diskCacheService)
    {
        this.databaseSeedService = databaseSeedService;
        this.scheduleMigrationService = scheduleMigrationService;
        this.alarmScheduleService = alarmScheduleService;
        this.dispatcher = dispatcher;
        this.bibleTranslationService = bibleTranslationService;
        this.bibleBookService = bibleBookService;
        this.mapper = mapper;
        this.mediaService = mediaService;
        this.melodyMusicService = melodyMusicService;
        this.diskCacheService = diskCacheService;
        this.statePopulator = new ScheduleStatePopulator(
            bibleTranslationService,
            bibleBookService,
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

            if (diskCacheService != null)
            {
                // Languages task is already running in parallel, await it now for the factory
                var languagesDict = await languagesTask;
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
                var languagesDict = await languagesTask;
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

            var schedulesElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - schedulesStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Schedule initialization completed in {ElapsedMs:F2}ms", schedulesElapsed);
#endif

#if ANDROID
            // Dispatch SetCarPlayScreenAction to fetch and set default schedule metadata for Android Auto
            // This will trigger the effect to fetch metadata and update MediaSession
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
        if (bibleTranslationService == null)
        {
            return null;
        }

        try
        {
            var languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
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
            includeBibleReading: true);

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
}

