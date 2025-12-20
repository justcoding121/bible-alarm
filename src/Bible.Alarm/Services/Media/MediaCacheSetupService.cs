using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaCacheSetupService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IMediaCacheSetupService
{

    public async Task SetupAlarmCacheAsync(int scheduleId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
            await mediaCacheService.SetupAlarmCacheAsync(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting up alarm cache for schedule {ScheduleId}", scheduleId);
        }
    }

}

