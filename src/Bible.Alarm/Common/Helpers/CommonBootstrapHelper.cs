using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
                });
                _servicesVerified = true;
                Log.Logger.Information("Database and IO operations completed");
            }
        });

        // Send InitializedMessage after lock is released
        if (initializeUI)
        {
            Log.Logger.Information("Sending InitializedMessage to trigger navigation");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ObservableMessenger.InitializationMessenger.Send(new InitializedMessage());
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
}