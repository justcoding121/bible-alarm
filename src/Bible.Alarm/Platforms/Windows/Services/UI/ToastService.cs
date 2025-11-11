using Bible.Alarm.Services.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Frame = Microsoft.UI.Xaml.Controls.Frame;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI
{
    public class WindowsToastService(TaskScheduler taskScheduler) : ToastService
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
                    .ContinueWith(async x =>
                        await ShowAlert(message, seconds), taskScheduler);
            }
            else
            {
                await ShowAlert(message, seconds);
            }
        }

        private static async Task ShowAlert(string message, double seconds)
        {
            clearRequest = new TaskCompletionSource<bool>();
            await Lock.WaitAsync();

            try
            {
                // Get the current window - handle null case
                var currentWindow = Window.Current;
                
                if (currentWindow == null)
                {
                    // If we don't have a window, just return (can't show toast without a window)
                    return;
                }

                var flyout = new Flyout
                {
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },

                    Placement = FlyoutPlacementMode.Bottom
                };

                // Try to get a FrameworkElement to attach the flyout to
                FrameworkElement targetElement = null;
                
                var currentFrame = currentWindow.Content as Frame;
                if (currentFrame != null)
                {
                    targetElement = currentFrame;
                }
                else
                {
                    // If Content is not a Frame, try to get it as FrameworkElement
                    targetElement = currentWindow.Content as FrameworkElement;
                }

                if (targetElement != null)
                {
                    flyout.OverlayInputPassThroughElement = targetElement;
                    flyout.ShowAt(targetElement);
                }
                else
                {
                    // If we can't find a suitable element, just return (can't show toast without a target)
                    return;
                }

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