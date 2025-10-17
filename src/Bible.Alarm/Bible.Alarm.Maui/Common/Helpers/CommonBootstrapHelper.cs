using Bible.Alarm.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Common.Helpers
{
    public static class CommonBootstrapHelper
    {
        private static SemaphoreSlim @lock = new SemaphoreSlim(1);
        public async static Task VerifyServices(IContainer container)
        {
            await @lock.WaitAsync();

            try
            {
                var task1 = verifyMediaLookUpService(container);
                var task2 = initializeDatabase(container);

                await Task.WhenAll(task1, task2);
            }
            finally
            {
                @lock.Release();
            }
        }

        private async static Task verifyMediaLookUpService(IContainer container)
        {
            using var service = container.Resolve<MediaIndexService>();
            await service.Verify();

        }

        private static async Task initializeDatabase(IContainer container)
        {
            using var db = container.Resolve<ScheduleDbContext>();
            await db.Database.MigrateAsync();
        }

    }
}
