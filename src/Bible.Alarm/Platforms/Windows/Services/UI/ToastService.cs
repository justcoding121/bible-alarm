using Bible.Alarm.Services.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Bible.Alarm.Platforms.Windows.Services.UI
{
    public class UwpToastService(TaskScheduler taskScheduler) : ToastService
    {
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        private static TaskCompletionSource<bool> clearRequest;

        public override Task Clear()
        {
            if (clearRequest != null)
            {
                clearRequest.SetResult(true);
            }

            return Task.CompletedTask;
        }

        public override async Task ShowMessage(string message, int seconds)
        {
            if (clearRequest != null)
            {
                return;
            }

            if (!MainThread.IsMainThread)
            {
                await Task.Delay(0)
                    .ContinueWith(async (x) =>
                        await ShowAlert(message, seconds), taskScheduler);
            }
            else
            {
                await ShowAlert(message, seconds);
            }
        }

        private async Task ShowAlert(string message, double seconds)
        {
            clearRequest = new TaskCompletionSource<bool>();
            await Lock.WaitAsync();

            try
            {
                var flyout = new Flyout
                {
                    Content = new TextBlock()
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },

                    Placement = FlyoutPlacementMode.Bottom
                };

                Microsoft.UI.Xaml.Controls.Frame currentFrame =
                    Microsoft.UI.Xaml.Window.Current.Content as Microsoft.UI.Xaml.Controls.Frame;
                flyout.OverlayInputPassThroughElement = currentFrame;
                flyout.ShowAt(currentFrame);

                await Task.WhenAny(clearRequest.Task, Task.Delay((int)(seconds * 1000))).ConfigureAwait(true);
                flyout.Hide();
            }
            finally
            {
                Lock.Release();
                clearRequest = null;
            }
        }
    }
}