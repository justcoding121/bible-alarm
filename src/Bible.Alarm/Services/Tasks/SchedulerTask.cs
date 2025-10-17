using Bible.Alarm.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.Services.Tasks
{
    public class SchedulerTask(
        ScheduleDbContext scheduleDbContext,
        IMediaCacheService mediaCacheService,
        IAlarmService alarmService,
        INotificationService notificationService,
        IStorageService storageService)
        : IDisposable
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);

        public async Task ProcessScheduledTasks()
        {
            await Handle();
        }

        public async Task<bool> Handle()
        {
            var downloaded = false;
            if (await @lock.WaitAsync(1000))
            {
                try
                {
                    try
                    {
                        await mediaCacheService.CleanUp();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "An error happenned inside cleanup task.");
                    }

                    var schedules = await scheduleDbContext.AlarmSchedules.Where(x => x.IsEnabled).ToListAsync();
                    foreach (var schedule in schedules)
                    {
                        if (!await notificationService.IsScheduled(schedule.Id))
                        {
                            downloaded = true;
                            await alarmService.Create(schedule);
                        }
                        else
                        {
                            downloaded = await mediaCacheService.SetupAlarmCache(schedule.Id);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to process scheduler task. Db directory: {storageService.CacheRoot}");
                }
                finally
                {
                    try
                    {
                        @lock.Release();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Logger.Error(e, "SchedulerTask: @lock disposed error.");
                    }
                }
            }
            return downloaded;
        }

        public void Dispose()
        {
            scheduleDbContext.Dispose();
            mediaCacheService.Dispose();
            alarmService.Dispose();
            notificationService.Dispose();
            storageService.Dispose();
        }
    }
}
