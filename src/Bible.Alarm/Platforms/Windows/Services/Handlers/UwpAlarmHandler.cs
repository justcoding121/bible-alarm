using Bible.Alarm.Services.Contracts;
using Serilog;

namespace Bible.Alarm.Services.Windows.Handlers
{
    public class UwpAlarmHandler : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<UwpAlarmHandler>();


        private IPlaybackService _playbackService;

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);

        public UwpAlarmHandler(IPlaybackService playbackService)
        {
            _playbackService = playbackService;
        }

        public async Task Handle(long scheduleId, bool isImmediate)
        {
            try
            {
                await @lock.WaitAsync();

                if (_playbackService.IsPrepared)
                {
                    Dispose();
                    return;
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        await _playbackService.PrepareAndPlay(scheduleId, isImmediate);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "An error happened when ringing the alarm.");
                        throw;
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when creating the task to ring the alarm.");
                Dispose();
            }
            finally
            {
                @lock.Release();
            }
        }

        public void Dispose()
        {
        }
    }
}