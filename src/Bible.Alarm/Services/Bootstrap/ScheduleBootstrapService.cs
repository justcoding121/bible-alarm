#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Common;
using Bible.Alarm.Stores;

using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IVocalMusicService? vocalMusicService;
    private readonly ScheduleStatePopulator statePopulator;
    private readonly IServiceScopeFactory scopeFactory;

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
        IVocalMusicService? vocalMusicService,
        IServiceScopeFactory scopeFactory)
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
        this.vocalMusicService = vocalMusicService;
        this.scopeFactory = scopeFactory;
        this.statePopulator = new ScheduleStatePopulator(
            BiblePublicationService,
            biblePublicationSectionService,
            mapper,
            mediaService,
            melodyMusicService,
            vocalMusicService,
            scopeFactory);
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

        // BibleGateway migration no longer needed - removed

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
            // If schedules are already loaded in Fluxor state, don't hit the DB again.
            // This prevents unnecessary DB work when the UI re-triggers bootstrap (e.g., navigating back to Home after Save).
            var existingState = ServiceProviderManager.GetService<Fluxor.IState<ApplicationState>>();
            if (existingState?.Value.Schedules != null && existingState.Value.Schedules.Count > 0)
            {
                Log.Logger.Debug("[BOOTSTRAP] Schedules already loaded in state ({Count}), skipping ScheduleBootstrapService.InitializeAsync", existingState.Value.Schedules.Count);
                return;
            }

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

            // No ScheduleList disk cache: always load from DB and populate display names deterministically.
            // This avoids “stale state” / “overwrite state shortly after save” issues.
            var languagesDict = await languagesTask;
#if DEBUG
            var languagesElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - languagesStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Loaded languages dictionary in {ElapsedMs:F2}ms", languagesElapsed);
#endif

            if (scheduleWasSeeded)
            {
                Log.Logger.Debug("[BOOTSTRAP] Schedule was seeded during bootstrap");
            }

            var schedulesList = await LoadSchedulesListAsync(languagesDict);
            var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
            foreach (var item in schedulesList)
            {
                initialSchedules.Add(item);
            }

#if DEBUG
            var dispatchStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            await DispatchInitializeActionAsync(initialSchedules);
#if DEBUG
            var dispatchElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dispatchStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Dispatched InitializeAction in {ElapsedMs:F2}ms", dispatchElapsed);
#endif

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
            Log.Logger.Information("Loaded {Count} languages for publication name lookup", languagesDict.Count);
            return languagesDict;
        }
        catch (Exception langEx)
        {
            Log.Logger.Warning(langEx, "Error loading languages - publication names will not be populated");
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
}

