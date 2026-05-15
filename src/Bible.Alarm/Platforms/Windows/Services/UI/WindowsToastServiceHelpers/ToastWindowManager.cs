#nullable enable
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Application = Microsoft.Maui.Controls.Application;
using Frame = Microsoft.UI.Xaml.Controls.Frame;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

/// <summary>
/// Manages window access for toast popups.
/// </summary>
internal static class ToastWindowManager
{
    public static Window? GetNativeWindow()
    {
        // First try Window.Current (works in some contexts). Release builds on CI / headless hosts
        // can throw REGDB_E_CLASSNOTREG when WinUI is not registered — treat as no static window.
        Window? currentWindow = null;
        try
        {
            currentWindow = Window.Current;
        }
        catch (COMException)
        {
        }

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

    public static FrameworkElement? FindTargetElement(Window currentWindow)
    {
        return currentWindow.Content switch
        {
            Frame frame => frame,
            FrameworkElement contentElement => contentElement,
            _ => FindFrameworkElementInVisualTree(currentWindow.Content)
        };
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
}

