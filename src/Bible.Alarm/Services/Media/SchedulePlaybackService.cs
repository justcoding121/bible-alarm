using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class SchedulePlaybackService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : ISchedulePlaybackService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task PlayScheduleAsync(int scheduleId)
    {
        if (scheduleId <= 0) return;

        using var scope = _scopeFactory.CreateScope();
        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        try
        {
            await playbackService.PrepareAndPlayAsync(scheduleId, false);
            await notificationService.ShowNotificationAsync(scheduleId);
        }
        catch (Exception e)
        {
            _logger.Information(e, "An error happened when playing alarm.");
            await toastService.ShowMessage("Error. Network may not be available. Please try again.", 5);
        }
    }

    public async Task<bool> CanMoveChapterAsync(int scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

        if (!playbackService.IsPreparingOrPlaying || playbackService.CurrentScheduleId != scheduleId) return true;

        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        await toastService.ShowMessage("Cannot update the chapter when schedule is in progress.");

        return false;
    }
}

