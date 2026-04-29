#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
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
            logger.Warning(AppConstants.Logging.ScheduleMediaCacheServiceDiagnosticsLog.SkippingMediaCacheSetupInvalidScheduleId,
                scheduleId);
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
                    
                    // Delete cache files that don't match the new schedule configuration
                    // This will compute the new schedule's URLs and delete only unmatched files
                    await mediaCacheService.DeleteScheduleCacheAsync(scheduleId);
                    
                    // Setup new cache for the updated schedule
                    await mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex,
                        AppConstants.Logging.ScheduleMediaCacheServiceDiagnosticsLog.ErrorDeletingOldCacheAndSettingUpNewCacheForSchedule,
                        scheduleId);
                }
            });
        }
        else
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await mediaCacheSetupService.SetupAlarmCacheAsync(scheduleId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex,
                        AppConstants.Logging.ScheduleMediaCacheServiceDiagnosticsLog.ErrorSettingUpMediaCacheForSchedule, scheduleId);
                }
            });
        }
    }
}


