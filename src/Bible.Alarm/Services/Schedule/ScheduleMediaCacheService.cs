#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleMediaCacheService : IScheduleMediaCacheService
{
    private readonly ILogger logger;
    private readonly IMediaCacheSetupService mediaCacheSetupService;
    private readonly IServiceProvider serviceProvider;

    public ScheduleMediaCacheService(
        ILogger logger,
        IMediaCacheSetupService mediaCacheSetupService,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.mediaCacheSetupService = mediaCacheSetupService;
        this.serviceProvider = serviceProvider;
    }

    public void SetupMediaCache(int scheduleId, bool isUpdate = false)
    {
        if (scheduleId <= 0)
        {
            logger.Warning("Skipping media cache setup for invalid schedule ID: {ScheduleId}", scheduleId);
            return;
        }

        if (isUpdate)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
                    await mediaCacheService.DeleteScheduleCacheAsync(scheduleId);
                    await mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error deleting old cache and setting up new cache for schedule {ScheduleId}", scheduleId);
                }
            });
        }
        else
        {
            _ = mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
        }
    }
}


