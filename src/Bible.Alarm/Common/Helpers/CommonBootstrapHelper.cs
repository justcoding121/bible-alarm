using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Database;
using Bible.Alarm.Services.Media;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Common.Helpers;

public static class CommonBootstrapHelper
{
    private static readonly SemaphoreSlim Lock = new(1);

    public static async Task VerifyServices()
    {
        await Lock.WaitAsync();

        try
        {
            var task1 = VerifyMediaLookUpService();
            var task2 = InitializeDatabase();

            await Task.WhenAll(task1, task2);
            
            // Send InitializedMessage right after common bootstrap is complete
            // This ensures databases are initialized before the message is sent
            MainThread.BeginInvokeOnMainThread(() =>
            {
                WeakReferenceMessenger.Default.Send(new InitializedMessage(true));
            });
        }
        finally
        {
            Lock.Release();
        }
    }

    private static async Task VerifyMediaLookUpService()
    {
        var service = ServiceProviderManager.GetService<MediaIndexService>();
        await service.Verify();
    }

    private static async Task InitializeDatabase()
    {
        await using var db = ServiceProviderManager.GetService<ScheduleDbContext>();
        await db.Database.MigrateAsync();
    }
}