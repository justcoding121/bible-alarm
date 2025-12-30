#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Serilog;
using Application = Microsoft.Maui.Controls.Application;
using Border = Microsoft.UI.Xaml.Controls.Border;
using Colors = Microsoft.UI.Colors;
using CornerRadius = Microsoft.UI.Xaml.CornerRadius;
using Frame = Microsoft.UI.Xaml.Controls.Frame;
using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using Thickness = Microsoft.UI.Xaml.Thickness;
using VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

public sealed partial class WindowsToastService(TaskScheduler taskScheduler, ILogger logger) : ToastService, IDisposable
{
    private bool isDisposed;
    private static readonly SemaphoreSlim @lock = new(1);

    private static TaskCompletionSource<bool>? clearRequest;
    private static Popup? currentPopup;
    private static Window? currentWindow;
    private static SizeChangedEventHandler? sizeChangedHandler;
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public override Task Clear()
    {
        if (clearRequest is { } request)
        {
            request.SetResult(true);
        }

        // Also close any currently open popup
        if (currentPopup != null)
        {
            try
            {
                currentPopup.IsOpen = false;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Exception occurred while closing popup in Clear()");
            }
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

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            try
            {
                await CloseExistingPopupIfNeededAsync();
                var currentWindow = GetNativeWindow();
                if (currentWindow is null)
                {
                    return;
                }

                var popup = CreateToastPopup(message, currentWindow);
                currentPopup = popup;
                await ShowFlyoutAsync(popup, currentWindow, seconds);
            }
            catch (COMException ex)
            {
                // COM exceptions can occur when manipulating UI elements from wrong thread or during cleanup
                Log.Warning(ex, "COM exception occurred while showing toast message. This can happen when manipulating UI elements from wrong thread or during cleanup");
            }
            finally
            {
                CleanupCurrentPopup();
            }
        });

        clearRequest = null;
    }

    private static async Task CloseExistingPopupIfNeededAsync()
    {
        // Close any existing popup first
        if (currentPopup != null)
        {
            try
            {
                currentPopup.IsOpen = false;
                // Give it time to close
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception occurred while closing existing popup before showing new one");
            }
            finally
            {
                CleanupPopup(currentPopup);
                currentPopup = null;
            }
        }
    }

    private static void CleanupCurrentPopup()
    {
        if (currentPopup != null)
        {
            CleanupPopup(currentPopup);
            currentPopup = null;
        }
    }

    private static Window? GetNativeWindow()
    {
        // First try Window.Current (works in some contexts)
        var currentWindow = Window.Current;

        // If Window.Current is null, try to get it from MAUI Application
        if (currentWindow is null)
        {
            currentWindow = GetWindowFromMauiApplication();
        }

        return currentWindow;
    }

    private static Window? GetWindowFromMauiApplication()
    {
        var windows = Application.Current?.Windows;
        if (windows is not null && windows.Count > 0)
        {
            var mauiWindow = windows[0];
            var handler = mauiWindow.Handler;
            if (handler?.PlatformView is Window nativeWindow)
            {
                return nativeWindow;
            }
        }

        return null;
    }

    private static FrameworkElement? FindTargetElement(Window currentWindow)
    {
        return currentWindow.Content switch
        {
            // First, try to get Frame (like legacy UWP code)
            Frame frame => frame,
            // If Content is not a Frame, try to get it as FrameworkElement
            FrameworkElement contentElement => contentElement,
            _ => FindFrameworkElementInVisualTree(currentWindow.Content)
        };

        // If we still don't have a target, try to find any FrameworkElement in the visual tree
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

    private static Popup CreateToastPopup(string message, Window currentWindow)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var backgroundColor = GetToastBackgroundColor(theme);
        var textColor = GetToastTextColor(theme);

        // Create a TextBlock for the message
        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(16, 12, 16, 12),
            Foreground = new SolidColorBrush(textColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            // Limit width for better appearance
            MaxWidth = 400
        };

        // Create a Border for the toast background
        var border = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            CornerRadius = new CornerRadius(8),
            Child = textBlock,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Create the Popup
        var popup = new Popup
        {
            Child = border,
            IsLightDismissEnabled = false,
            ShouldConstrainToRootBounds = true
        };

        return popup;
    }

    private static Windows.UI.Color GetToastBackgroundColor(AppTheme theme)
    {
        // For dark theme: use a lighter dark background for better contrast
        // For light theme: use dark background with opacity for visibility
        if (theme == AppTheme.Dark)
        {
            // Use a lighter dark color that contrasts well with dark backgrounds
            // #2A2A2A with 95% opacity (0xF2 = 242/255 ≈ 95%)
            return Windows.UI.Color.FromArgb(0xF2, 0x2A, 0x2A, 0x2A);
        }
        else
        {
            // Use dark background with opacity for light theme (standard toast style)
            return Windows.UI.Color.FromArgb(0xCC, 0x00, 0x00, 0x00); // Black with 80% opacity (0xCC = 204/255)
        }
    }

    private static Windows.UI.Color GetToastTextColor(AppTheme theme)
    {
        // White text works well on both dark and semi-transparent dark backgrounds
        return Colors.White;
    }

    private static async Task ShowFlyoutAsync(Popup popup, Window currentWindow, double seconds)
    {
        FrameworkElement? windowContent = null;

        try
        {
            windowContent = SetupPopupAndGetWindowContent(popup, currentWindow);
            WindowsToastService.currentWindow = currentWindow;

            SetInitialPopupPosition(popup, windowContent);
            popup.IsOpen = true;

            await Task.Delay(100); // Wait for render
            UpdatePopupPosition(popup, currentWindow);

            SubscribeToWindowSizeChanges(windowContent, popup, currentWindow);
            await WaitForDisplayDuration(seconds);
        }
        catch (COMException ex)
        {
            // COM exceptions can occur when manipulating UI elements
            Log.Warning(ex, "COM exception occurred while showing popup flyout. Closing popup and continuing");
        }
        finally
        {
            await CleanupFlyoutResources(currentWindow, popup);
        }
    }

    private static FrameworkElement? SetupPopupAndGetWindowContent(Popup popup, Window currentWindow)
    {
        // Attach popup to the window and get window content for positioning
        // Safely access window content - it may not be accessible during navigation
        try
        {
            if (currentWindow?.Content is FrameworkElement content)
            {
                // Set the popup's XamlRoot
                if (content.XamlRoot != null)
                {
                    popup.XamlRoot = content.XamlRoot;
                }
                return content;
            }
        }
        catch (COMException ex)
        {
            // Window content may not be accessible if window is being disposed or during navigation
            Log.Debug(ex, "Window content not accessible when setting up popup (window may be disposed)");
            // Continue without setting XamlRoot - popup may not work but won't crash
        }

        return null;
    }

    private static void SetInitialPopupPosition(Popup popup, FrameworkElement? windowContent)
    {
        if (windowContent != null)
        {
            var windowWidth = windowContent.ActualWidth > 0 ? windowContent.ActualWidth : 400;
            var windowHeight = windowContent.ActualHeight > 0 ? windowContent.ActualHeight : 600;
            // Estimate popup size (will be adjusted after render)
            var estimatedPopupWidth = 300;
            var estimatedPopupHeight = 50;

            popup.HorizontalOffset = (windowWidth - estimatedPopupWidth) / 2;
            popup.VerticalOffset = windowHeight - estimatedPopupHeight - 50;
        }
    }

    private static void SubscribeToWindowSizeChanges(FrameworkElement? windowContent, Popup popup, Window currentWindow)
    {
        if (windowContent != null)
        {
            sizeChangedHandler = (_, _) =>
            {
                try
                {
                    UpdatePopupPosition(popup, currentWindow);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Exception occurred while updating popup position on window resize");
                }
            };
            windowContent.SizeChanged += sizeChangedHandler;
        }
    }

    private static async Task WaitForDisplayDuration(double seconds)
    {
        if (clearRequest is { } request)
        {
            await Task.WhenAny(request.Task, Task.Delay((int)(seconds * 1000)));
        }
        else
        {
            await Task.Delay((int)(seconds * 1000));
        }
    }

    private static async Task CleanupFlyoutResources(Window currentWindow, Popup popup)
    {
        // Unsubscribe from size changes - safely access window content
        if (sizeChangedHandler != null)
        {
            try
            {
                // Safely access window content - it may be disposed during navigation
                if (currentWindow?.Content is FrameworkElement content)
                {
                    content.SizeChanged -= sizeChangedHandler;
                }
            }
            catch (COMException ex)
            {
                // Window may be disposed or in invalid state during navigation
                Log.Debug(ex, "Window content not accessible during cleanup (window may be disposed)");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception occurred while unsubscribing from window size changed event");
            }
            finally
            {
                sizeChangedHandler = null;
            }
        }
        WindowsToastService.currentWindow = null;

        try
        {
            if (popup.IsOpen)
            {
                popup.IsOpen = false;
            }
        }
        catch (COMException ex)
        {
            // Popup may be disposed or in invalid state - this is expected during cleanup
            Log.Debug(ex, "Popup not accessible during cleanup (popup may be disposed)");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception occurred while closing popup in ShowFlyoutAsync finally block");
        }

        // Clean up after a brief delay to allow animation to complete
        await Task.Delay(200);
        CleanupPopup(popup);
    }

    private static void UpdatePopupPosition(Popup popup, Window? currentWindow)
    {
        if (currentWindow?.Content is FrameworkElement content && popup.Child is Border border)
        {
            var windowWidth = content.ActualWidth;
            var windowHeight = content.ActualHeight;
            var borderWidth = border.ActualWidth;
            var borderHeight = border.ActualHeight;

            if (windowWidth > 0 && windowHeight > 0 && borderWidth > 0 && borderHeight > 0)
            {
                try
                {
                    // Center horizontally, position at bottom (50px from bottom)
                    popup.HorizontalOffset = (windowWidth - borderWidth) / 2;
                    popup.VerticalOffset = windowHeight - borderHeight - 50;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Exception occurred while updating popup position");
                }
            }
        }
    }

    private static void CleanupPopup(Popup? popup)
    {
        if (popup == null)
        {
            return;
        }

        try
        {
            popup.Child = null;
        }
        catch (COMException ex)
        {
            // Popup may be disposed or in invalid state - this is expected during cleanup
            Log.Debug(ex, "Popup not accessible during cleanup (popup may be disposed)");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Exception occurred while clearing popup child");
        }

        // Don't try to set XamlRoot to null - it can fail if already set or popup is disposed
        // The popup will be garbage collected anyway, and setting it to null can cause COM exceptions
    }

    public override void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // TaskScheduler is a singleton, so don't dispose it
        // No event handlers to unsubscribe
        base.Dispose();
    }
}
