using Bible.Alarm.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Common.Helpers
{
    public static class CommonBootstrapHelper
    {
        private static SemaphoreSlim @lock = new SemaphoreSlim(1);
        public static async Task VerifyServices()
        {
            await @lock.WaitAsync();

            try
            {
                var task1 = VerifyMediaLookUpService();
                var task2 = InitializeDatabase();

                await Task.WhenAll(task1, task2);
            }
            finally
            {
                @lock.Release();
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
}
