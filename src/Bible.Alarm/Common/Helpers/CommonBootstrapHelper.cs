#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Effects;
#endif

namespace Bible.Alarm.Common.Helpers;

public static class CommonBootstrapHelper
{
    private static readonly SemaphoreSlim @lock = new(1);
    private static volatile bool servicesVerified;

    public static async Task VerifyServices(bool initializeUi = false)
    {
        Log.Logger.Information("VerifyServices called with initializeUI={InitializeUI}, _servicesVerified={ServicesVerified}",
            initializeUi, servicesVerified);

        // Track if we need to send early navigation after database/Fluxor are ready
        var shouldSendEarlyNav = false;
        
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (servicesVerified)
            {
                Log.Logger.Information("Services already verified, skipping database operations");
            }
            else
            {
#if DEBUG
                var dbOpsStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
                Log.Logger.Information("[BOOTSTRAP] Starting database and IO operations");
#endif
                // Track if we should send early navigation (only for UI initialization)
                shouldSendEarlyNav = initializeUi;
                
                // Run database and IO operations off UI thread
                await Task.Run(async () =>
                {
#if DEBUG
                    var verifyMediaStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                    var task1 = VerifyMediaLookUpService();
                    var task2 = InitializeDatabase();
                    var task3 = InitializeFluxorStore();
                    var task4 = CopySilentMp3ToStorage();

                    await Task.WhenAll(task1, task2, task3, task4);
#if DEBUG
                    var verifyMediaElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - verifyMediaStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    Log.Logger.Information("[BOOTSTRAP] Media index verification/copy completed in {ElapsedMs:F2}ms", verifyMediaElapsed);
#endif

                    // Send InitializedMessage early (after database/Fluxor are ready) to show UI with loading state
                    // This improves perceived performance - user sees the home page while schedules are being populated
                    if (shouldSendEarlyNav)
                    {
#if DEBUG
                        var earlyNavStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
                        Log.Logger.Information("[BOOTSTRAP] Sending InitializedMessage early (before schedule population)");
#endif
                        try
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    WeakReferenceMessenger.Default.Send(new InitializedMessage());
#if DEBUG
                                    var earlyNavElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - earlyNavStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                                    Log.Logger.Information("[BOOTSTRAP] Early InitializedMessage sent - Navigation triggered in {ElapsedMs:F2}ms", earlyNavElapsed);
#endif
                                }
                                catch (Exception ex)
                                {
                                    Log.Logger.Error(ex, "Error sending early InitializedMessage");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error(ex, "Error invoking MainThread for early InitializedMessage");
                        }
                    }

                    // After database and Fluxor store are initialized, load schedules into state
                    // This ensures schedules are available for both Android Auto services and main UI
                    // UI is already showing (via early InitializedMessage), so user sees loading state
                    await InitializeSchedules();
                });
                servicesVerified = true;
#if DEBUG
                var dbOpsElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dbOpsStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Logger.Information("[BOOTSTRAP] Database and IO operations completed in {ElapsedMs:F2}ms", dbOpsElapsed);
#endif
            }
        });

        // Send InitializedMessage after lock is released (only if not already sent early)
        // NavigateToHomeAsync handles duplicate navigation attempts internally
        // CRITICAL: Send InitializedMessage even if services were already verified
        // This handles the case where Android Auto completed bootstrap first (isForeground=false)
        // and the UI needs to navigate away from BootstrapPage
        if (initializeUi && shouldSendEarlyNav)
        {
            // Services just verified, and we already sent early InitializedMessage above
            // So we don't need to send it again here
            Log.Logger.Debug("InitializedMessage already sent early, skipping duplicate send");
        }
        else if (initializeUi && servicesVerified)
        {
            // Services were already verified (bootstrap completed by Android Auto or previous call)
            // Since bootstrap is complete, handlers should already be registered, so send immediately
#if DEBUG
            var navStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
            Log.Logger.Information("[BOOTSTRAP] Sending InitializedMessage immediately (services verified: {ServicesVerified}, bootstrap complete)", servicesVerified);
#endif
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        WeakReferenceMessenger.Default.Send(new InitializedMessage());
#if DEBUG
                        var navElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                        Log.Logger.Information("[BOOTSTRAP] InitializedMessage sent immediately - Navigation triggered in {ElapsedMs:F2}ms", navElapsed);
#endif
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error sending InitializedMessage (immediate)");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error invoking MainThread for immediate InitializedMessage");
            }
        }
        else
        {
            Log.Logger.Information("initializeUI=false, not sending InitializedMessage");
        }
    }

    private static async Task VerifyMediaLookUpService()
    {
        var service = ServiceProviderManager.GetService<IMediaIndexService>();
        await service.Verify();
    }

    private static async Task CopyScheduleDatabaseFromResourceIfNeeded(IServiceScope scope)
    {
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var storageService = ServiceProviderManager.GetService<IStorageService>();
        var scheduleVersionService = ServiceProviderManager.GetService<IScheduleDatabaseVersionService>();
        
        var dbPath = scheduleDb.Database.GetDbConnection().DataSource;
        var dbExists = System.IO.File.Exists(dbPath);
        
        // If database doesn't exist, copy from bundled resource
        if (!dbExists)
        {
            // Use the same filename as the database file for consistency
            var scheduleDbResourceFile = AppConstants.Database.ScheduleDatabaseFileName;
            var dbDirectory = System.IO.Path.GetDirectoryName(dbPath);
            
            if (!string.IsNullOrEmpty(dbDirectory) && !System.IO.Directory.Exists(dbDirectory))
            {
                System.IO.Directory.CreateDirectory(dbDirectory);
            }
            
            try
            {
                // Copy bundled empty database from resources
                await storageService.CopyResourceFile(
                    scheduleDbResourceFile, 
                    dbDirectory ?? storageService.StorageRoot, 
                    System.IO.Path.GetFileName(dbPath));
                
                Log.Logger.Information("[BOOTSTRAP] Copied Schedule database from bundled resource");
                
                // Don't save version here - let migration check verify the database schema is correct
                // The migration check will be fast if schema exists, and will create it if missing
            }
            catch (Exception ex)
            {
                // If resource copy fails (e.g., resource not found), fall back to migrations
                Log.Logger.Debug(ex, 
                    "Failed to copy Schedule database from resource, will create with migrations instead");
            }
        }
    }

    private static async Task InitializeDatabase()
    {
#if DEBUG
        var dbInitStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Log.Logger.Information("[BOOTSTRAP] Database initialization starting");
#endif
        
        // Create a scope for the DbContext since it's registered as scoped
        // This ensures proper lifetime management and prevents disposal issues
        var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
        await using var scope = scopeFactory.CreateAsyncScope();

        // Migrate Schedule database (always safe - app owns this DB)
        // Optimize: Copy from bundled resource on first launch, or check version to skip migration check
#if DEBUG
        var scheduleDbStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var scheduleVersionService = ServiceProviderManager.GetService<IScheduleDatabaseVersionService>();
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        // Check if database file exists
        var dbPath = scheduleDb.Database.GetDbConnection().DataSource;
        var dbExists = System.IO.File.Exists(dbPath);
        
        // If database doesn't exist, try copying from bundled resource first
        // This eliminates the need for migrations on clean install (saves ~1.2 seconds)
        if (!dbExists)
        {
            await CopyScheduleDatabaseFromResourceIfNeeded(scope);
            dbExists = System.IO.File.Exists(dbPath); // Re-check after copy attempt
        }
        
        // Even if version matches, we still need to verify the schema exists
        // (database file might be corrupted, empty, or missing schema)
        // GetPendingMigrationsAsync() is fast if schema exists (just reads migrations history table)
        var versionMatches = dbExists && await scheduleVersionService.IsVersionCurrentAsync();
        
        if (versionMatches)
        {
            Log.Logger.Debug("[BOOTSTRAP] Schedule database version matches current app version, verifying schema...");
        }
        else
        {
            // Version mismatch, first launch, or database doesn't exist
            if (!dbExists)
            {
                Log.Logger.Debug("[BOOTSTRAP] Schedule database file does not exist, will be created from bundled resource or migrations");
            }
            else
            {
                Log.Logger.Debug("[BOOTSTRAP] Schedule database version mismatch or not set, will verify schema and apply migrations if needed");
            }
        }
        
        // Always verify schema by checking for pending migrations
        // This is fast if schema exists (just reads migrations history table)
        // If bundled database was copied correctly, this should return empty (schema already exists)
        // If bundled database is missing/empty/corrupted, this will return all migrations (need to create schema)
        try
        {
            var pendingScheduleMigrations = await scheduleDb.Database.GetPendingMigrationsAsync();
            if (pendingScheduleMigrations.Any())
            {
                Log.Logger.Information(
                    "[BOOTSTRAP] Schedule database has {Count} pending migrations, applying...",
                    pendingScheduleMigrations.Count());
                await scheduleDb.Database.MigrateAsync();
                Log.Logger.Information("[BOOTSTRAP] Schedule database migrations applied successfully");
            }
            else
            {
                if (versionMatches)
                {
                    Log.Logger.Debug("[BOOTSTRAP] Schedule database schema verified - version matches and all migrations applied");
                }
                else
                {
                    Log.Logger.Debug("[BOOTSTRAP] Schedule database is already up to date (bundled database had schema), skipping migration");
                }
            }
        }
        catch (Exception ex)
        {
            // If GetPendingMigrationsAsync fails (e.g., database is corrupted), try to migrate
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to check pending migrations, attempting to migrate database");
            try
            {
                await scheduleDb.Database.MigrateAsync();
                Log.Logger.Information("[BOOTSTRAP] Schedule database migration completed after error recovery");
            }
            catch (Exception migrateEx)
            {
                Log.Logger.Error(migrateEx, "[BOOTSTRAP] Failed to migrate Schedule database, database may be corrupted");
                throw;
            }
        }
        
        // Save current version after successful schema verification/migration
        // This marks the database as verified for the current app version
        if (!versionMatches)
        {
            await scheduleVersionService.SaveCurrentVersionAsync();
        }
#if DEBUG
        var scheduleDbElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - scheduleDbStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Schedule database migration completed in {ElapsedMs:F2}ms", scheduleDbElapsed);
#endif

        // Migrate Media database if it exists and is from a previous app version
        // Note: App is packaged with latest media index database, so this primarily
        // handles users upgrading from previous app versions
#if DEBUG
        var mediaDbStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var mediaMigrationService = ServiceProviderManager.GetService<IMediaMigrationService>();
        await mediaMigrationService.MigrateIfNeededAsync();
#if DEBUG
        var mediaDbElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - mediaDbStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Media database migration completed in {ElapsedMs:F2}ms", mediaDbElapsed);
        
        var dbInitElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dbInitStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Database initialization completed in {ElapsedMs:F2}ms", dbInitElapsed);
#endif
    }

    private static async Task InitializeFluxorStore()
    {
#if DEBUG
        var fluxorStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Log.Logger.Information("[BOOTSTRAP] Fluxor store initialization starting");
#endif

        // Get the store from service provider
        var store = ServiceProviderManager.GetService<IStore>();
        if (store == null)
        {
            Log.Logger.Warning("IStore service not found - Fluxor store initialization skipped");
            return;
        }

        // Initialize store asynchronously
        await store.InitializeAsync();

        // Set the static store reference
        ReduxContainer.Store = store;
        
#if DEBUG
        var fluxorElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - fluxorStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Fluxor store initialization completed in {ElapsedMs:F2}ms", fluxorElapsed);
#endif

#if ANDROID
        // Android Auto can start the process without constructing the MAUI App UI (App.xaml.cs),
        // so register MediaSessionEffect message handlers here to ensure progress updates flow to MediaSession.
        // (Metadata/status/navigation are handled via Fluxor effects, but position comes from MVVM messages.)
        try
        {
            var mediaSessionEffect = ServiceProviderManager.GetService<MediaSessionEffect>();
            mediaSessionEffect?.RegisterMessageHandlers();
            Log.Logger.Debug("MediaSessionEffect message handlers registered (bootstrap)");
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to register MediaSessionEffect message handlers (bootstrap)");
        }
#endif

        Log.Logger.Information("Fluxor store initialized successfully");
    }

    private static async Task InitializeSchedules()
    {
#if DEBUG
        var schedulesStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Log.Logger.Information("[BOOTSTRAP] Schedule initialization starting");
#endif

        try
        {
            var services = GetRequiredServices();
            if (services == null)
            {
                return;
            }

            // Parallelize seed/migration with language loading
            // Languages can load independently while we seed/migrate schedules
#if DEBUG
            var seedStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
            var languagesStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            var seedTask = SeedAndMigrateSchedules(services);
            var languagesTask = LoadLanguagesDictionary(services.BibleTranslationService);
            
            // Wait for seed to complete before loading schedules (schedules may be created during seed)
            var scheduleWasSeeded = await seedTask;
#if DEBUG
            var seedElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - seedStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Schedule seed/migration completed in {ElapsedMs:F2}ms", seedElapsed);
#endif
            
            // Load schedules from cache or factory
            // Cache stores as List<ScheduleStateItem> for JSON serialization
            var cacheService = ServiceProviderManager.GetService<IDiskCacheService>();
            const string CacheKey = "ScheduleList";
            
#if DEBUG
            var cacheStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ObservableHashSet<ScheduleStateItem> initialSchedules;
            
            if (cacheService != null)
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
                    cacheService.Remove(CacheKey);
                }
                
                // Use cache with factory - factory will be called if cache miss or deserialization fails
                var cachedSchedulesList = await cacheService.GetOrSetAsync(
                    CacheKey,
                    async () =>
                    {
                        // Factory: Load schedules from database and populate state items
                        return await LoadSchedulesListAsyncInternal(services, languagesDict);
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
                var schedulesList = await LoadSchedulesListAsyncInternal(services, languagesDict);
                initialSchedules = new ObservableHashSet<ScheduleStateItem>();
                foreach (var item in schedulesList)
                {
                    initialSchedules.Add(item);
                }
            }

#if DEBUG
            var dispatchStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            await DispatchInitializeAction(services.Dispatcher, initialSchedules);
#if DEBUG
            var dispatchElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dispatchStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Dispatched InitializeAction in {ElapsedMs:F2}ms", dispatchElapsed);
            
            var schedulesElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - schedulesStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Logger.Information("[BOOTSTRAP] Schedule initialization completed in {ElapsedMs:F2}ms", schedulesElapsed);
#endif

#if ANDROID
            // Dispatch SetCarPlayScreenAction to fetch and set default schedule metadata for Android Auto
            // This will trigger the effect to fetch metadata and update MediaSession
            services.Dispatcher.Dispatch(new SetCarPlayScreenAction());
            Log.Logger.Debug("SetCarPlayScreenAction dispatched after bootstrap completion");
#endif
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error initializing schedules in bootstrap");
        }
    }

    public record BootstrapServices(
        IDatabaseSeedService DatabaseSeedService,
        IScheduleMigrationService ScheduleMigrationService,
        IAlarmScheduleService AlarmScheduleService,
        IDispatcher Dispatcher,
        IBibleTranslationService BibleTranslationService,
        IBibleBookService BibleBookService,
        IMapper Mapper,
        IMediaService MediaService,
        IMelodyMusicService MelodyMusicService);

    /// <summary>
    /// Gets required services for bootstrap operations.
    /// </summary>
    public static BootstrapServices? GetRequiredServices()
    {
        return GetRequiredServicesInternal();
    }
    
    /// <summary>
    /// Gets required services for cache refresh operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public static BootstrapServices? GetRequiredServicesForCache()
    {
        return GetRequiredServicesInternal();
    }
    
    /// <summary>
    /// Loads languages dictionary for cache refresh operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public static async Task<Dictionary<string, Language>?> LoadLanguagesDictionaryForCache(IBibleTranslationService? bibleTranslationService)
    {
        return await LoadLanguagesDictionary(bibleTranslationService);
    }
    
    /// <summary>
    /// Loads schedules list for cache refresh operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public static async Task<List<ScheduleStateItem>> LoadSchedulesListAsyncForCache(
        BootstrapServices services,
        Dictionary<string, Language>? languagesDict)
    {
        return await LoadSchedulesListAsyncInternal(services, languagesDict);
    }
    
    private static BootstrapServices? GetRequiredServicesInternal()
    {
        var databaseSeedService = ServiceProviderManager.GetService<IDatabaseSeedService>();
        var scheduleMigrationService = ServiceProviderManager.GetService<IScheduleMigrationService>();
        var alarmScheduleService = ServiceProviderManager.GetService<IAlarmScheduleService>();
        var dispatcher = ServiceProviderManager.GetService<IDispatcher>();
        var bibleTranslationService = ServiceProviderManager.GetService<IBibleTranslationService>();
        var bibleBookService = ServiceProviderManager.GetService<IBibleBookService>();
        var mapper = ServiceProviderManager.GetService<IMapper>();
        var mediaService = ServiceProviderManager.GetService<IMediaService>();
        var melodyMusicService = ServiceProviderManager.GetService<IMelodyMusicService>();

        if (databaseSeedService == null || scheduleMigrationService == null ||
            alarmScheduleService == null || dispatcher == null)
        {
            Log.Logger.Warning("Required services not available for schedule initialization - skipping");
            return null;
        }

        return new BootstrapServices(
            databaseSeedService,
            scheduleMigrationService,
            alarmScheduleService,
            dispatcher,
            bibleTranslationService!,
            bibleBookService!,
            mapper!,
            mediaService!,
            melodyMusicService!);
    }

    private static async Task<bool> SeedAndMigrateSchedules(BootstrapServices services)
    {
        bool scheduleWasSeeded = false;
        try
        {
            // Seed default schedule if database is empty
            // Schema is guaranteed to exist at this point (verified in InitializeDatabase)
            scheduleWasSeeded = await services.DatabaseSeedService.SeedDefaultAlarmAsync();
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
            await services.ScheduleMigrationService.MigrateBibleGatewaySchedulesAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't fail bootstrap
            Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to migrate Bible Gateway schedules, continuing bootstrap");
        }
        
        return scheduleWasSeeded;
    }

    private static async Task<List<AlarmSchedule>> LoadSchedulesFromDatabase(IAlarmScheduleService alarmScheduleService)
    {
        var alarmSchedules = await alarmScheduleService.GetAllSchedulesAsync(
            includeMusic: true,
            includeBibleReading: true);

        Log.Logger.Information("Loaded {Count} schedules from database during bootstrap", alarmSchedules.Count);
        return alarmSchedules;
    }
    
    /// <summary>
    /// Factory method to load schedules from database and populate state items.
    /// This is used by the cache service to populate the cache on cache miss.
    /// Returns List for JSON serialization (converted to ObservableHashSet by caller).
    /// </summary>
    private static async Task<List<ScheduleStateItem>> LoadSchedulesListAsyncInternal(
        BootstrapServices services,
        Dictionary<string, Language>? languagesDict)
    {
#if DEBUG
        var loadStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var alarmSchedules = await LoadSchedulesFromDatabase(services.AlarmScheduleService);
#if DEBUG
        var loadElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - loadStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Loaded {Count} schedules from database in {ElapsedMs:F2}ms", alarmSchedules.Count, loadElapsed);
#endif
        
#if DEBUG
        var populateStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var scheduleStateItems = await PopulateScheduleStateItems(
            alarmSchedules,
            services,
            languagesDict);
#if DEBUG
        var populateElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - populateStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Populated {Count} schedule state items in {ElapsedMs:F2}ms", scheduleStateItems.Count, populateElapsed);
#endif
        
        // Convert ObservableHashSet to List for JSON serialization
        return scheduleStateItems.ToList();
    }

    private static async Task<Dictionary<string, Language>?> LoadLanguagesDictionary(IBibleTranslationService? bibleTranslationService)
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

    private static async Task<ObservableHashSet<ScheduleStateItem>> PopulateScheduleStateItems(
        List<AlarmSchedule> alarmSchedules,
        BootstrapServices services,
        Dictionary<string, Language>? languagesDict)
    {
        // Optimize: Batch load all required data upfront to avoid N+1 queries
        var lookupData = await LoadAllLookupDataAsync(alarmSchedules, services);

        // Process schedules in parallel instead of sequentially
        var scheduleTasks = alarmSchedules.Select(async schedule =>
        {
            var scheduleStateItem = services.Mapper.Map<ScheduleStateItem>(schedule);

            // Use pre-loaded lookup data instead of making individual queries
            PopulateBibleReadingDisplayNamesFromCache(
                schedule,
                scheduleStateItem,
                lookupData,
                languagesDict);

            PopulateMusicDisplayNamesFromCache(
                schedule,
                scheduleStateItem,
                lookupData);

            return scheduleStateItem;
        });

        var scheduleStateItems = await Task.WhenAll(scheduleTasks);
        
        // Batch populate default music for all schedules that need it
        await PopulateDefaultMusicBatchAsync(
            alarmSchedules,
            scheduleStateItems,
            services);

        var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in scheduleStateItems)
        {
            initialSchedules.Add(item);
        }
        return initialSchedules;
    }

    /// <summary>
    /// Batch loads all required lookup data upfront to avoid N+1 queries.
    /// </summary>
    private static async Task<LookupData> LoadAllLookupDataAsync(
        List<AlarmSchedule> alarmSchedules,
        BootstrapServices services)
    {
        // Collect all unique keys needed
        var translationKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var bookKeys = new HashSet<(string LanguageCode, string PublicationCode, int BookNumber)>();
        var vocalMusicLanguageCodes = new HashSet<string>();
        var vocalMusicKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var vocalTrackKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var melodyPublicationCodes = new HashSet<string>();

        foreach (var schedule in alarmSchedules)
        {
            // Collect Bible reading keys
            if (schedule.BibleReadingSchedule != null)
            {
                var br = schedule.BibleReadingSchedule;
                if (!string.IsNullOrWhiteSpace(br.LanguageCode) && !string.IsNullOrWhiteSpace(br.PublicationCode))
                {
                    translationKeys.Add((br.LanguageCode, br.PublicationCode));
                    if (br.BookNumber > 0)
                    {
                        bookKeys.Add((br.LanguageCode, br.PublicationCode, br.BookNumber));
                    }
                }
            }

            // Collect music keys
            if (schedule.Music != null)
            {
                var music = schedule.Music;
                if (music.MusicType == Shared.Models.Enums.MusicType.Vocals)
                {
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode))
                    {
                        vocalMusicLanguageCodes.Add(music.LanguageCode);
                        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                        {
                            vocalMusicKeys.Add((music.LanguageCode, music.PublicationCode));
                            if (music.TrackNumber > 0)
                            {
                                vocalTrackKeys.Add((music.LanguageCode, music.PublicationCode));
                            }
                        }
                    }
                }
                else if (music.MusicType == Shared.Models.Enums.MusicType.Melodies)
                {
                    if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        melodyPublicationCodes.Add(music.PublicationCode);
                    }
                }
            }
        }

        // Load all data in parallel
        var translationTasks = translationKeys.Select(async key =>
        {
            try
            {
                var translation = await services.BibleTranslationService?.GetByLanguageAndCodeWithBooksAsync(
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Translation: translation);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading translation {LanguageCode}/{PublicationCode}", 
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Translation: (BibleTranslation?)null);
            }
        }).ToList();

        var bookTasks = bookKeys.Select(async key =>
        {
            try
            {
                var bookName = await services.BibleBookService?.GetBookNameAsync(
                    key.LanguageCode, key.PublicationCode, key.BookNumber);
                return (Key: key, BookName: bookName);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading book {LanguageCode}/{PublicationCode}/{BookNumber}", 
                    key.LanguageCode, key.PublicationCode, key.BookNumber);
                return (Key: key, BookName: (string?)null);
            }
        }).ToList();

        var vocalLanguagesTask = services.MediaService != null && vocalMusicLanguageCodes.Any()
            ? services.MediaService.GetVocalMusicLanguages()
            : Task.FromResult<Dictionary<string, Language>>(new Dictionary<string, Language>());

        var vocalReleasesTasks = vocalMusicKeys.GroupBy(k => k.LanguageCode).Select(async group =>
        {
            try
            {
                var releases = await services.MediaService?.GetVocalMusicReleases(group.Key);
                return (LanguageCode: group.Key, Releases: releases ?? new Dictionary<string, VocalMusic>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal music releases for {LanguageCode}", group.Key);
                return (LanguageCode: group.Key, Releases: new Dictionary<string, VocalMusic>());
            }
        }).ToList();

        var vocalTracksTasks = vocalTrackKeys.Select(async key =>
        {
            try
            {
                var tracks = await services.MediaService?.GetVocalMusicTracks(key.LanguageCode, key.PublicationCode);
                return (Key: key, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal tracks {LanguageCode}/{PublicationCode}", 
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        var melodyTracksTasks = melodyPublicationCodes.Select(async pubCode =>
        {
            try
            {
                var tracks = await services.MediaService?.GetMelodyMusicTracks(pubCode);
                return (PublicationCode: pubCode, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading melody tracks {PublicationCode}", pubCode);
                return (PublicationCode: pubCode, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        // Wait for all batch loads to complete in parallel
        await Task.WhenAll(
            Task.WhenAll(translationTasks),
            Task.WhenAll(bookTasks),
            vocalLanguagesTask,
            Task.WhenAll(vocalReleasesTasks),
            Task.WhenAll(vocalTracksTasks),
            Task.WhenAll(melodyTracksTasks));

        // Build lookup dictionaries
        var translationsDict = translationTasks
            .Where(t => t.Result.Translation != null)
            .ToDictionary(t => t.Result.Key, t => t.Result.Translation!);

        var booksDict = bookTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Result.BookName))
            .ToDictionary(t => t.Result.Key, t => t.Result.BookName!);

        var vocalLanguagesDict = await vocalLanguagesTask;

        var vocalReleasesDict = (await Task.WhenAll(vocalReleasesTasks))
            .SelectMany(r => r.Releases.Select(kvp => new { Key = (r.LanguageCode, PublicationCode: kvp.Key), Release = kvp.Value }))
            .ToDictionary(x => x.Key, x => x.Release);

        var vocalTracksDict = (await Task.WhenAll(vocalTracksTasks))
            .ToDictionary(t => t.Key, t => t.Tracks);

        var melodyTracksDict = (await Task.WhenAll(melodyTracksTasks))
            .ToDictionary(t => t.PublicationCode, t => t.Tracks);

        return new LookupData(
            Translations: translationsDict,
            Books: booksDict,
            VocalLanguages: vocalLanguagesDict,
            VocalReleases: vocalReleasesDict,
            VocalTracks: vocalTracksDict,
            MelodyTracks: melodyTracksDict);
    }

    /// <summary>
    /// Lookup data structure for batch-loaded display names.
    /// </summary>
    private sealed record LookupData(
        Dictionary<(string LanguageCode, string PublicationCode), BibleTranslation> Translations,
        Dictionary<(string LanguageCode, string PublicationCode, int BookNumber), string> Books,
        Dictionary<string, Language> VocalLanguages,
        Dictionary<(string LanguageCode, string PublicationCode), VocalMusic> VocalReleases,
        Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>> VocalTracks,
        Dictionary<string, SortedDictionary<int, MusicTrack>> MelodyTracks);

    /// <summary>
    /// Populates Bible reading display names using pre-loaded lookup data.
    /// </summary>
    private static void PopulateBibleReadingDisplayNamesFromCache(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupData lookupData,
        Dictionary<string, Language>? languagesDict)
    {
        if (schedule.BibleReadingSchedule == null)
        {
            return;
        }

        var bibleReading = schedule.BibleReadingSchedule;
        SetBibleReadingLanguageName(schedule, scheduleStateItem, bibleReading, languagesDict);

        // Use cached translation
        if (!string.IsNullOrWhiteSpace(bibleReading.LanguageCode) && 
            !string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
        {
            var translationKey = (bibleReading.LanguageCode, bibleReading.PublicationCode);
            if (lookupData.Translations.TryGetValue(translationKey, out var translation) &&
                !string.IsNullOrWhiteSpace(translation.Name))
            {
                scheduleStateItem.BibleReadingPublicationName = translation.Name;
                Log.Logger.Debug("Set BibleReadingPublicationName '{BibleReadingPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    translation.Name, schedule.Id, bibleReading.PublicationCode);
            }
        }

        // Use cached book name
        if (bibleReading.BookNumber > 0 &&
            !string.IsNullOrWhiteSpace(bibleReading.LanguageCode) &&
            !string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
        {
            var bookKey = (bibleReading.LanguageCode, bibleReading.PublicationCode, bibleReading.BookNumber);
            if (lookupData.Books.TryGetValue(bookKey, out var bookName))
            {
                scheduleStateItem.BibleReadingBookName = bookName;
                Log.Logger.Debug("Set BibleReadingBookName '{BibleReadingBookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                    bookName, schedule.Id, bibleReading.BookNumber);
            }
        }
    }

    /// <summary>
    /// Populates music display names using pre-loaded lookup data.
    /// </summary>
    private static void PopulateMusicDisplayNamesFromCache(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupData lookupData)
    {
        if (schedule.Music == null)
        {
            return;
        }

        var music = schedule.Music;

        // Use cached vocal languages
        if (music.MusicType == Shared.Models.Enums.MusicType.Vocals &&
            !string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            if (lookupData.VocalLanguages.TryGetValue(music.LanguageCode, out var vocalLanguage))
            {
                scheduleStateItem.MusicLanguageName = vocalLanguage.Name;
                Log.Logger.Debug("Set MusicLanguageName '{MusicLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    vocalLanguage.Name, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
            }

            // Use cached vocal releases
            if (!string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                var releaseKey = (music.LanguageCode, music.PublicationCode);
                if (lookupData.VocalReleases.TryGetValue(releaseKey, out var release))
                {
                    scheduleStateItem.MusicPublicationName = release.Name;
                    Log.Logger.Debug("Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        release.Name, schedule.Id, music.PublicationCode);
                }
            }

            // Use cached vocal tracks
            if (music.TrackNumber > 0 &&
                !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                !string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                var trackKey = (music.LanguageCode, music.PublicationCode);
                if (lookupData.VocalTracks.TryGetValue(trackKey, out var tracks) &&
                    tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    scheduleStateItem.MusicTrackName = track.Title;
                    Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                        track.Title, schedule.Id, music.TrackNumber);
                }
            }
        }
        else if (music.MusicType == Shared.Models.Enums.MusicType.Melodies &&
                 !string.IsNullOrWhiteSpace(music.PublicationCode) &&
                 music.TrackNumber > 0)
        {
            // Use cached melody tracks
            if (lookupData.MelodyTracks.TryGetValue(music.PublicationCode, out var tracks) &&
                tracks.TryGetValue(music.TrackNumber, out var track))
            {
                scheduleStateItem.MusicTrackName = $"Melody Number(s) {track.Title}";
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, schedule.Id, music.TrackNumber);
            }
        }
    }

    private static void SetBibleReadingLanguageName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        Dictionary<string, Language>? languagesDict)
    {
        if (languagesDict == null)
        {
            return;
        }

        var languageCode = bibleReading.LanguageCode;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return;
        }

        if (languagesDict.TryGetValue(languageCode, out var language))
        {
            scheduleStateItem.BibleReadingLanguageName = language.Name;
            Log.Logger.Debug("Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                language.Name, schedule.Id, languageCode);
        }
        else
        {
            scheduleStateItem.BibleReadingLanguageName = languageCode;
            Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                languageCode, schedule.Id);
        }
    }


    /// <summary>
    /// Batch populates default music for all schedules that need it.
    /// This is more efficient than calling PopulateDefaultMusicIfNeeded for each schedule individually.
    /// </summary>
    private static async Task PopulateDefaultMusicBatchAsync(
        List<AlarmSchedule> alarmSchedules,
        ScheduleStateItem[] scheduleStateItems,
        BootstrapServices services)
    {
        // Identify schedules that need default music
        var schedulesNeedingMusic = new List<(AlarmSchedule Schedule, ScheduleStateItem StateItem)>();
        for (int i = 0; i < alarmSchedules.Count && i < scheduleStateItems.Length; i++)
        {
            var stateItem = scheduleStateItems[i];
            if (!stateItem.MusicType.HasValue ||
                !stateItem.MusicTrackNumber.HasValue ||
                stateItem.MusicTrackNumber.Value <= 0)
            {
                schedulesNeedingMusic.Add((alarmSchedules[i], stateItem));
            }
        }

        if (schedulesNeedingMusic.Count == 0)
        {
            return;
        }

        try
        {
            const string defaultPublicationCode = "iam";

            if (services.MelodyMusicService == null)
            {
                return;
            }

            // Load default music once for all schedules
            var melodyMusic = await services.MelodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

            if (melodyMusic?.Tracks == null || melodyMusic.Tracks.Count == 0)
            {
                Log.Logger.Warning("Melody music '{PublicationCode}' not found or has no tracks - cannot populate default music for {Count} schedules",
                    defaultPublicationCode, schedulesNeedingMusic.Count);
                return;
            }

            // Apply to all schedules needing music
            var random = new Random();
            foreach (var (schedule, stateItem) in schedulesNeedingMusic)
            {
                var randomTrack = melodyMusic.Tracks[random.Next(melodyMusic.Tracks.Count)];

                stateItem.MusicType = Shared.Models.Enums.MusicType.Melodies;
                stateItem.MusicPublicationCode = defaultPublicationCode;
                stateItem.MusicLanguageCode = null;
                stateItem.MusicTrackNumber = randomTrack.Number;
                stateItem.MusicRepeat = false;
                stateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";

                Log.Logger.Debug("Populated default music properties for schedule {ScheduleId}. MusicType=Melodies, PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
                    schedule.Id, defaultPublicationCode, randomTrack.Number);
            }

            Log.Logger.Information("Batch populated default music for {Count} schedules", schedulesNeedingMusic.Count);
        }
        catch (Exception defaultMusicEx)
        {
            Log.Logger.Warning(defaultMusicEx, "Error batch populating default music properties for {Count} schedules",
                schedulesNeedingMusic.Count);
        }
    }

    private static async Task DispatchInitializeAction(IDispatcher dispatcher, ObservableHashSet<ScheduleStateItem> initialSchedules)
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
    /// Copies silent.mp3 from embedded resources to storage directory (same as schedule database).
    /// This ensures the file is available for Android Auto dummy tracks.
    /// Only runs on Android platform. Fast exits if file already exists.
    /// </summary>
    private static async Task CopySilentMp3ToStorage()
    {
#if ANDROID
        try
        {
            const string ResourceFileName = "silent.mp3";
            var storageService = ServiceProviderManager.GetService<IStorageService>();

            // Copy to StorageRoot (same directory as schedule database) instead of CacheRoot
            // because cache can get deleted by the system
            var storageDir = storageService.StorageRoot;
            var filePath = Path.Combine(storageDir, ResourceFileName);

            // Fast exit: Check if file already exists synchronously first
            if (File.Exists(filePath))
            {
                Log.Logger.Debug("Silent MP3 already exists in storage: {FilePath}", filePath);
                return;
            }

            // Copy from embedded resource to storage directory
            await storageService.CopyResourceFile(ResourceFileName, storageDir, ResourceFileName);
            Log.Logger.Information("Silent MP3 copied to storage: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to copy silent MP3 to storage - will attempt to copy on-demand");
        }
#else
        // Only needed on Android for Android Auto dummy tracks
        await Task.CompletedTask;
#endif
    }

}
