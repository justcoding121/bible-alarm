using Bible.Alarm.Common.Interfaces.Media;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.Handlers
{
    public class WindowsAlarmHandler(ILogger logger, IPlaybackService playbackService) : IDisposable
    {
        private readonly ILogger _logger = logger;


        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        public async Task Handle(long scheduleId, bool isImmediate)
        {
            try
            {
                await Lock.WaitAsync();

                if (playbackService.IsPrepared)
                {
                    Dispose();
                    return;
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        await playbackService.PrepareAndPlay(scheduleId, isImmediate);
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