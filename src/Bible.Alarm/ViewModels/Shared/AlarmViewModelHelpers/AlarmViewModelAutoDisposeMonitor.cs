#nullable enable

using Bible.Alarm.Stores;
using Fluxor;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

internal static class AlarmViewModelAutoDisposeMonitor
{
    internal static void Start(IState<PlaybackState> playbackState, Func<bool> isDisposed, Action dispose)
    {
        Task.Run(async () =>
        {
            while (!isDisposed())
            {
                await Task.Delay(1000);

                var isRunning = playbackState.Value.IsPreparingOrPlaying;

                var count = 6;
                while (!isRunning && count > 0)
                {
                    await Task.Delay(500);
                    isRunning = playbackState.Value.IsPreparingOrPlaying;
                    count--;
                }

                if (!isRunning)
                {
                    dispose();
                }
            }
        });
    }
}

