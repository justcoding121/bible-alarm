using Bible.Alarm.Services;
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
        }
        finally
        {
            Lock.Release();
        }
    }

    private static async Task VerifyMediaLookUpService()
    {
        using var service = ServiceProviderManager.GetService<MediaIndexService>();
        await service.Verify();
    }

    private static async Task InitializeDatabase()
    {
        using var db = ServiceProviderManager.GetService<ScheduleDbContext>();
        await db.Database.MigrateAsync();
    }
}