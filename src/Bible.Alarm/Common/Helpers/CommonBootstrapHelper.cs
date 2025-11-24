using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Database;
using Bible.Alarm.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

public static class CommonBootstrapHelper
{
    private static readonly SemaphoreSlim Lock = new(1);
    private static volatile bool _initializationMessageSent = false;

    public static async Task VerifyServices()
    {
        await Lock.WaitAsync().ConfigureAwait(false);

        try
        {
            var task1 = VerifyMediaLookUpService();
            var task2 = InitializeDatabase();

            await Task.WhenAll(task1, task2).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log error but don't prevent app from starting
            // Use global Serilog logger instead of service provider to avoid dependency issues
            Log.Logger.Error(ex, "Error during bootstrap verification");
        }
        finally
        {
            // Always send InitializedMessage once, even if bootstrap had errors
            // This ensures the app can still navigate to Home page
            // Use ObservableMessenger so the message is not lost if recipient registers late
            // Only send once to prevent multiple navigation attempts
            if (!_initializationMessageSent)
            {
                _initializationMessageSent = true;
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
        var service = ServiceProviderManager.GetService<MediaIndexService>();
        await service.Verify().ConfigureAwait(false);
    }

    private static async Task InitializeDatabase()
    {
        // Create a scope for the DbContext since it's registered as scoped
        // This ensures proper lifetime management and prevents disposal issues
        var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }
}