using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Common.Helpers;

public static class CommonBootstrapHelper
{
    private static readonly SemaphoreSlim Lock = new(1);
    private static volatile bool _servicesVerified = false;
    
    public static async Task VerifyServices(bool initializeUI = false)
    {
        Log.Logger.Information("VerifyServices called with initializeUI={InitializeUI}, _servicesVerified={ServicesVerified}", 
            initializeUI, _servicesVerified);
        
        await ConcurrencyHelper.ExecuteAsync(Lock, async () =>
        {
            if (_servicesVerified)
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

                    await Task.WhenAll(task1, task2, task3);
                    
                    // After database and Fluxor store are initialized, load schedules into state
                    // This ensures schedules are available for both Android Auto services and main UI
                    await InitializeSchedules();
                });
                _servicesVerified = true;
                Log.Logger.Information("Database and IO operations completed");
            }
        });

        // Send InitializedMessage after lock is released
        // NavigateToHomeAsync handles duplicate navigation attempts internally
        if (initializeUI)
        {
            Log.Logger.Information("Sending InitializedMessage to trigger navigation");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                WeakReferenceMessenger.Default.Send(new InitializedMessage());
                Log.Logger.Information("InitializedMessage sent");
            });
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
            Dictionary<string, Bible.Alarm.Shared.Models.Media.Language>? languagesDict = null;
            var bibleTranslationService = ServiceProviderManager.GetService<IBibleTranslationService>();
            if (bibleTranslationService != null)
            {
                try
                {
                    languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
                    Log.Logger.Information("Loaded {Count} languages for translation name lookup", languagesDict.Count);
                }
                catch (Exception langEx)
                {
                    Log.Logger.Warning(langEx, "Error loading languages - translation names will not be populated");
                }
            }
            else
            {
                Log.Logger.Warning("IBibleTranslationService is null - translation names will not be populated");
            }
            
            // Populate translation names for each schedule's BibleReadingSchedule
            foreach (var schedule in alarmSchedules)
            {
                if (schedule.BibleReadingSchedule != null && languagesDict != null)
                {
                    var languageCode = schedule.BibleReadingSchedule.LanguageCode;
                    if (!string.IsNullOrWhiteSpace(languageCode) && 
                        languagesDict.TryGetValue(languageCode, out var language))
                    {
                        schedule.BibleReadingSchedule.TranslationName = language.Name;
                        Log.Logger.Debug("Set TranslationName '{TranslationName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                            language.Name, schedule.Id, languageCode);
                    }
                    else
                    {
                        // Fallback to language code if language not found
                        schedule.BibleReadingSchedule.TranslationName = languageCode;
                        Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as TranslationName for schedule {ScheduleId}",
                            languageCode, schedule.Id);
                    }
                }
            }
            
            // Create ObservableHashSet for state
            var initialSchedules = new ObservableHashSet<AlarmSchedule>();
            foreach (var schedule in alarmSchedules)
            {
                initialSchedules.Add(schedule);
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
}
