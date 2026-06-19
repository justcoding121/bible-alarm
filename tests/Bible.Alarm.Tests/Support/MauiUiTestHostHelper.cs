#nullable enable

namespace Bible.Alarm.Tests.Support;

internal static class MauiUiTestHostHelper
{
    /// <summary>
    /// True when MAUI bootstrap succeeded and a visual tree host (Window) is available or can be created.
    /// On Android/iOS device hosts, <see cref="Application.Current"/> exists but <see cref="Application.Windows"/>
    /// is often empty until a Window is added explicitly.
    /// </summary>
    public static bool CanUseVisualTree
    {
        get
        {
            if (!MauiUiTestBootstrap.IsReady || Application.Current is null)
            {
                return false;
            }

            if (Application.Current.Windows.Count > 0)
            {
                return true;
            }

            return EnsurePrimaryWindow();
        }
    }

    /// <summary>
    /// Ensures <see cref="Application.Current"/> has at least one <see cref="Window"/> for visual-tree tests.
    /// </summary>
    public static bool EnsurePrimaryWindow()
    {
        if (Application.Current is null)
        {
            return false;
        }

        if (Application.Current.Windows.Count > 0)
        {
            return true;
        }

        void AddWindowIfNeeded()
        {
            if ((Application.Current?.Windows.Count ?? 0) == 0 && Application.Current is not null)
            {
                Application.Current.OpenWindow(new Window(new NavigationPage(new ContentPage())));
            }
        }

        if (MainThread.IsMainThread)
        {
            AddWindowIfNeeded();
            return Application.Current.Windows.Count > 0;
        }

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            AddWindowIfNeeded();
            return Task.FromResult((Application.Current?.Windows.Count ?? 0) > 0);
        }).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Drains posted <see cref="MainThread"/> callbacks so assertions run after BeginInvokeOnMainThread work.
    /// </summary>
    public static Task FlushMainThreadAsync() =>
        MainThread.InvokeOnMainThreadAsync(() => Task.CompletedTask);
}
