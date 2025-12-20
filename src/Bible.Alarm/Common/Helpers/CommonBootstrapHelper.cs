#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
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

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (servicesVerified)
            {
                Log.Logger.Information("Services already verified, skipping database operations");
            }
            else
            {
                Log.Logger.Information("Starting database and IO operations");
                // Run database and IO operations off UI thread
                await Task.Run(async () =>
                {
                    var task1 = VerifyMediaLookUpService();
                    var task2 = InitializeDatabase();
                    var task3 = InitializeFluxorStore();
                    var task4 = CopySilentMp3ToStorage();

                    await Task.WhenAll(task1, task2, task3, task4);

                    // After database and Fluxor store are initialized, load schedules into state
                    // This ensures schedules are available for both Android Auto services and main UI
                    await InitializeSchedules();
                });
                servicesVerified = true;
                Log.Logger.Information("Database and IO operations completed");
            }
        });

        // Send InitializedMessage after lock is released
        // NavigateToHomeAsync handles duplicate navigation attempts internally
        // CRITICAL: Send InitializedMessage even if services were already verified
        // This handles the case where Android Auto completed bootstrap first (isForeground=false)
        // and the UI needs to navigate away from BootstrapPage
        if (initializeUi)
        {
            Log.Logger.Information("Sending InitializedMessage to trigger navigation (services verified: {ServicesVerified})", servicesVerified);

            // If services were already verified (bootstrap completed by Android Auto), add a small delay
            // to ensure MessageHandlingService.RegisterMessageHandlers() has been called in App.xaml.cs
            if (servicesVerified)
            {
                // Small delay to ensure message handlers are registered before sending message
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // 100ms delay to ensure handlers are registered
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        WeakReferenceMessenger.Default.Send(new InitializedMessage());
                        Log.Logger.Information("InitializedMessage sent (delayed for handler registration)");
                    });
                });
            }
            else
            {
                // Services just verified, send message immediately
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    WeakReferenceMessenger.Default.Send(new InitializedMessage());
                    Log.Logger.Information("InitializedMessage sent");
                });
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

    private static async Task InitializeDatabase()
    {
        // Create a scope for the DbContext since it's registered as scoped
        // This ensures proper lifetime management and prevents disposal issues
        var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
        await using var scope = scopeFactory.CreateAsyncScope();

        // Migrate Schedule database (always safe - app owns this DB)
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        await scheduleDb.Database.MigrateAsync();

        // Migrate Media database if it exists and is from a previous app version
        // Note: App is packaged with latest media index database, so this primarily
        // handles users upgrading from previous app versions
        var mediaMigrationService = ServiceProviderManager.GetService<IMediaMigrationService>();
        await mediaMigrationService.MigrateIfNeededAsync();
    }

    private static async Task InitializeFluxorStore()
    {
        Log.Logger.Information("Initializing Fluxor store");

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
        Log.Logger.Information("Initializing schedules in state");

        try
        {
            // Get services needed for schedule initialization
            var databaseSeedService = ServiceProviderManager.GetService<IDatabaseSeedService>();
            var scheduleMigrationService = ServiceProviderManager.GetService<IScheduleMigrationService>();
            var alarmScheduleService = ServiceProviderManager.GetService<IAlarmScheduleService>();
            var dispatcher = ServiceProviderManager.GetService<IDispatcher>();

            if (databaseSeedService == null || scheduleMigrationService == null ||
                alarmScheduleService == null || dispatcher == null)
            {
                Log.Logger.Warning("Required services not available for schedule initialization - skipping");
                return;
            }

            // Run seed and migration first (same as HomeViewModel)
            await databaseSeedService.SeedDefaultAlarmAsync();
            await scheduleMigrationService.MigrateBibleGatewaySchedulesAsync();

            // Load all schedules from database
            var alarmSchedules = await alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: true,
                includeBibleReading: true);

            Log.Logger.Information("Loaded {Count} schedules from database during bootstrap", alarmSchedules.Count);

            // Load language dictionary for translation names
            Dictionary<string, Language>? languagesDict = null;
            var bibleTranslationService = ServiceProviderManager.GetService<IBibleTranslationService>();
            try
            {
                languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
                Log.Logger.Information("Loaded {Count} languages for translation name lookup", languagesDict.Count);
            }
            catch (Exception langEx)
            {
                Log.Logger.Warning(langEx, "Error loading languages - translation names will not be populated");
            }

            // Get BibleBookService for book name lookup
            var bibleBookService = ServiceProviderManager.GetService<IBibleBookService>();

            // Get AutoMapper instance
            var mapper = ServiceProviderManager.GetService<IMapper>();

            // Create ObservableHashSet of ScheduleStateItem for state using AutoMapper
            // Include TranslationName from language dictionary and BookName from BibleBookService
            var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
            foreach (var schedule in alarmSchedules)
            {
                // Map AlarmSchedule to ScheduleStateItem using AutoMapper
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(schedule);

                // Set TranslationName and BookName from services
                if (schedule.BibleReadingSchedule != null)
                {
                    var bibleReading = schedule.BibleReadingSchedule;

                    // Set TranslationName from language dictionary
                    if (languagesDict != null)
                    {
                        var languageCode = bibleReading.LanguageCode;
                        if (!string.IsNullOrWhiteSpace(languageCode) &&
                            languagesDict.TryGetValue(languageCode, out var language))
                        {
                            scheduleStateItem.TranslationName = language.Name;
                            Log.Logger.Debug("Set TranslationName '{TranslationName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                                language.Name, schedule.Id, languageCode);
                        }
                        else
                        {
                            // Fallback to language code if language not found
                            scheduleStateItem.TranslationName = languageCode;
                            Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as TranslationName for schedule {ScheduleId}",
                                languageCode, schedule.Id);
                        }
                    }

                    // Set BookName from BibleBookService
                    if (bibleBookService != null && bibleReading.BookNumber > 0)
                    {
                        try
                        {
                            var bookName = await bibleBookService.GetBookNameAsync(
                                bibleReading.LanguageCode,
                                bibleReading.PublicationCode,
                                bibleReading.BookNumber);

                            if (!string.IsNullOrWhiteSpace(bookName))
                            {
                                scheduleStateItem.BookName = bookName;
                                Log.Logger.Debug("Set BookName '{BookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                                    bookName, schedule.Id, bibleReading.BookNumber);
                            }
                        }
                        catch (Exception bookEx)
                        {
                            Log.Logger.Warning(bookEx, "Error loading book name for schedule {ScheduleId} (BookNumber: {BookNumber})",
                                schedule.Id, bibleReading.BookNumber);
                        }
                    }
                }

                initialSchedules.Add(scheduleStateItem);
            }

            // Dispatch InitializeAction to populate state
            // This must be done on main thread since Fluxor dispatcher may require UI context
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                dispatcher.Dispatch(new InitializeAction(initialSchedules));
                Log.Logger.Information("Dispatched InitializeAction with {Count} schedules", initialSchedules.Count);
            });
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error initializing schedules in bootstrap");
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
