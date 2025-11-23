#nullable enable

using System.Linq;
using Bible.Alarm.Services.UI;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Frame = Microsoft.UI.Xaml.Controls.Frame;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI
{
    public class WindowsToastService : ToastService
    {
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        private static TaskCompletionSource<bool>? clearRequest;
        private readonly TaskScheduler _taskScheduler;

        public WindowsToastService(TaskScheduler taskScheduler)
        {
            _taskScheduler = taskScheduler;
        }

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
                    .ContinueWith(async _ =>
                        await ShowAlert(message, seconds), _taskScheduler);
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
                var currentWindow = GetNativeWindow();
                if (currentWindow == null)
                {
                    return;
                }

                var targetElement = FindTargetElement(currentWindow);
                if (targetElement == null)
                {
                    return;
                }

                var flyout = CreateFlyout(message);
                await ShowFlyoutAsync(flyout, targetElement, seconds);
            }
            finally
            {
                Lock.Release();
                clearRequest = null;
            }
        }

        private static Window? GetNativeWindow()
        {
            // First try Window.Current (works in some contexts)
            var currentWindow = Window.Current;
            
            // If Window.Current is null, try to get it from MAUI Application
            if (currentWindow == null)
            {
                var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
                if (mauiWindow != null)
                {
                    var handler = mauiWindow.Handler;
                    if (handler?.PlatformView is Window nativeWindow)
                    {
                        currentWindow = nativeWindow;
                    }
                }
            }
            
            return currentWindow;
        }

        private static FrameworkElement? FindTargetElement(Window currentWindow)
        {
            // First, try to get Frame (like legacy UWP code)
            if (currentWindow.Content is Frame frame)
            {
                return frame;
            }

            // If Content is not a Frame, try to get it as FrameworkElement
            if (currentWindow.Content is FrameworkElement contentElement)
            {
                return contentElement;
            }

            // If we still don't have a target, try to find any FrameworkElement in the visual tree
            return FindFrameworkElementInVisualTree(currentWindow.Content);
        }

        private static FrameworkElement? FindFrameworkElementInVisualTree(object? content)
        {
            if (content is not DependencyObject depObj)
            {
                return null;
            }

            var parent = VisualTreeHelper.GetParent(depObj);
            while (parent != null)
            {
                if (parent is FrameworkElement frameworkElement)
                {
                    return frameworkElement;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private static Flyout CreateFlyout(string message)
        {
            return new Flyout
            {
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Microsoft.UI.Xaml.Thickness(12)
                },
                Placement = FlyoutPlacementMode.Bottom,
                LightDismissOverlayMode = LightDismissOverlayMode.On
            };
        }

        private static async Task ShowFlyoutAsync(Flyout flyout, FrameworkElement targetElement, double seconds)
        {
            flyout.OverlayInputPassThroughElement = targetElement;
            flyout.ShowAt(targetElement);

            if (clearRequest != null)
            {
                await Task.WhenAny(clearRequest.Task, Task.Delay((int)(seconds * 1000))).ConfigureAwait(true);
            }
            else
            {
                await Task.Delay((int)(seconds * 1000));
            }
            flyout.Hide();
        }

    }
}