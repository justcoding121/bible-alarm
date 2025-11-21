using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaCacheSetupService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IMediaCacheSetupService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task SetupAlarmCacheAsync(int scheduleId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mediaCacheService = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
            await mediaCacheService.SetupAlarmCacheAsync(scheduleId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting up alarm cache for schedule {ScheduleId}", scheduleId);
        }
    }
}

