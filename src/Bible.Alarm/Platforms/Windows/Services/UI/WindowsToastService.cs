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
    public sealed partial class WindowsToastService(TaskScheduler taskScheduler) : ToastService
    {
        private static readonly SemaphoreSlim Lock = new(1);

        private static TaskCompletionSource<bool>? clearRequest;
        private readonly TaskScheduler _taskScheduler = taskScheduler;

        public override Task Clear()
        {
            if (clearRequest is { } request)
            {
                request.SetResult(true);
            }

            return Task.CompletedTask;
        }

        public override async Task ShowMessage(string message, int seconds)
        {
            if (clearRequest is not null)
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
                if (currentWindow is null)
                {
                    return;
                }

                var targetElement = FindTargetElement(currentWindow);
                if (targetElement is null)
                {
                    return;
                }

                var teachingTip = CreateTeachingTip(message, targetElement);
                await ShowFlyoutAsync(teachingTip, targetElement, seconds);
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
            if (currentWindow is null)
            {
                var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
                if (windows is not null && windows.Count > 0)
                {
                    var mauiWindow = windows[0];
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
            while (parent is not null)
            {
                if (parent is FrameworkElement frameworkElement)
                {
                    return frameworkElement;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private static TeachingTip CreateTeachingTip(string message, FrameworkElement targetElement)
        {
            var teachingTip = new TeachingTip
            {
                Title = message,
                IsLightDismissEnabled = true,
                PreferredPlacement = TeachingTipPlacementMode.Bottom,
                Target = targetElement,
                IsOpen = false
            };
            
            return teachingTip;
        }

        private static async Task ShowFlyoutAsync(TeachingTip teachingTip, FrameworkElement targetElement, double seconds)
        {
            // Ensure TeachingTip is in the visual tree by adding it to the window's content
            var currentWindow = GetNativeWindow();
            Panel? containerPanel = null;
            
            if (currentWindow?.Content is FrameworkElement windowContent)
            {
                // Try to find or create a container for the TeachingTip
                if (windowContent is Panel panel)
                {
                    containerPanel = panel;
                }
                else if (windowContent is ContentControl contentControl && contentControl.Content is Panel contentPanel)
                {
                    containerPanel = contentPanel;
                }
                
                // If we found a panel, add the TeachingTip to it
                if (containerPanel != null && !containerPanel.Children.Contains(teachingTip))
                {
                    containerPanel.Children.Add(teachingTip);
                }
            }
            
            teachingTip.IsOpen = true;

            if (clearRequest is { } request)
            {
                await Task.WhenAny(request.Task, Task.Delay((int)(seconds * 1000))).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay((int)(seconds * 1000));
            }
            
            teachingTip.IsOpen = false;
            
            // Clean up after a brief delay to allow animation to complete
            await Task.Delay(200);
            
            // Remove from visual tree
            if (containerPanel != null && containerPanel.Children.Contains(teachingTip))
            {
                containerPanel.Children.Remove(teachingTip);
            }
            
            teachingTip.Target = null;
        }

    }
}