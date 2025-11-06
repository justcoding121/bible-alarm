using Bible.Alarm.Common.Infrastructure.Schedule;
using Bible.Alarm.Services.Infrastructure.Schedule;
using Bible.Alarm.Services.Media;
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
        var service = ServiceProviderManager.GetService<MediaIndexService>();
        await service.Verify();
    }

    private static async Task InitializeDatabase()
    {
        await using var db = ServiceProviderManager.GetService<ScheduleDbContext>();
        await db.Database.MigrateAsync();
    }
}