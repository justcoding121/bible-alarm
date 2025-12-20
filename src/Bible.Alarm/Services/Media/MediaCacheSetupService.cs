using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaCacheSetupService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IMediaCacheSetupService, IDisposable
{
    private readonly ILogger logger = logger;
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private bool isDisposed;

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

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

