using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.Handlers;

public class WindowsAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IState<PlaybackState> playbackState) : IWindowsAlarmHandler
{
    private readonly ILogger _logger = logger;
    private readonly IState<PlaybackState> _playbackState = playbackState;


    private static readonly SemaphoreSlim @lock = new(1);

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
            {
                if (_playbackState.Value.IsPreparingOrPlaying)
                {
                    Dispose();
                    return;
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "An error happened when ringing the alarm.");
                        throw;
                    }
                });
            });
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened when creating the task to ring the alarm.");
            Dispose();
        }
    }

    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Dispose static semaphore
        try
        {
            @lock.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore if already disposed
            _logger.Warning(ex, "Error disposing semaphore, may already be disposed");
        }

        // All injected services (playbackService, IState<PlaybackState>) are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
