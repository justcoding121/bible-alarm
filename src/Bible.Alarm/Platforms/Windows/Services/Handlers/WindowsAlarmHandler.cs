using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.Handlers
{
    public class WindowsAlarmHandler(
        ILogger logger, 
        IPlaybackService playbackService,
        IState<PlaybackState> playbackState) : IDisposable
    {
        private readonly ILogger _logger = logger;
        private readonly IState<PlaybackState> _playbackState = playbackState;


        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        public async Task HandleAsync(int scheduleId, bool isAlarm)
        {
            try
            {
                await Lock.WaitAsync();

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
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened when creating the task to ring the alarm.");
                Dispose();
            }
            finally
            {
                Lock.Release();
            }
        }

        public void Dispose()
        {
        }
    }
}