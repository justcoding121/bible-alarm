using Bible.Alarm.Services;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Xamarin.Essentials;

namespace JW.Alarm.Services.UWP
{
    public class UwpToastService : ToastService
    {
        private readonly TaskScheduler taskScheduler;

        public UwpToastService(TaskScheduler taskScheduler)
        {
            this.taskScheduler = taskScheduler;
        }

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);

        private static TaskCompletionSource<bool> clearRequest;
        public override Task Clear()
        {
            if (clearRequest != null)
            {
                clearRequest.SetResult(true);
            }

            return Task.CompletedTask;
        }

        public async override Task ShowMessage(string message, int seconds)
        {
            if (clearRequest != null)
            {
                return;
            }

            if (!MainThread.IsMainThread)
            {
                await Task.Delay(0)
                   .ContinueWith(async (x) =>
                       await showAlert(message, (double)seconds), taskScheduler);
            }
            else
            {
                await showAlert(message, (double)seconds);
            }
        }

        private async Task showAlert(string message, double seconds)
        {
            clearRequest = new TaskCompletionSource<bool>();
            await @lock.WaitAsync();

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

                Frame currentFrame = Window.Current.Content as Frame;
                flyout.OverlayInputPassThroughElement = currentFrame;
                flyout.ShowAt(currentFrame);

                await Task.WhenAny(clearRequest.Task, Task.Delay((int)(seconds * 1000))).ConfigureAwait(true);
                flyout.Hide();

            }
            finally
            {
                @lock.Release();
                clearRequest = null;
            }
        }
    }
}
