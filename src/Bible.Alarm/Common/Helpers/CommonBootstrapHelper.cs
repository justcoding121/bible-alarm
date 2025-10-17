using Bible.Alarm.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Common.Helpers
{
    public static class CommonBootstrapHelper
    {
        private static SemaphoreSlim @lock = new SemaphoreSlim(1);
        public static async Task VerifyServices(IContainer container)
        {
            await @lock.WaitAsync();

            try
            {
                var task1 = VerifyMediaLookUpService(container);
                var task2 = InitializeDatabase(container);

                await Task.WhenAll(task1, task2);
            }
            finally
            {
                @lock.Release();
            }
        }

        private static async Task VerifyMediaLookUpService(IContainer container)
        {
            using var service = container.Resolve<MediaIndexService>();
            await service.Verify();

        }

        private static async Task InitializeDatabase(IContainer container)
        {
            using var db = container.Resolve<ScheduleDbContext>();
            await db.Database.MigrateAsync();
        }

    }
}
