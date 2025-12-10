using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

public static class CommonBootstrapHelper
{
    private static readonly SemaphoreSlim Lock = new(1);
    public static async Task VerifyServices(bool initializeUI = false)
    {
        await Lock.WaitAsync();

        try
        {
            // Run database and IO operations off UI thread
            await Task.Run(async () =>
            {
                var task1 = VerifyMediaLookUpService();
                var task2 = InitializeDatabase();

                await Task.WhenAll(task1, task2);
            });
        }
        catch (Exception ex)
        {
            // Log error but don't prevent app from starting
            // Use global Serilog logger instead of service provider to avoid dependency issues
            Log.Logger.Error(ex, "Error during bootstrap verification");
        }
        finally
        {
            if (initializeUI)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ObservableMessenger.InitializationMessenger.Send(new InitializedMessage());
                });
            }
            
            Lock.Release();
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
}