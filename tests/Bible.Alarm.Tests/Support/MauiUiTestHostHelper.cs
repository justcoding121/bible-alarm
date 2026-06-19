#nullable enable

namespace Bible.Alarm.Tests.Support;

internal static class MauiUiTestHostHelper
{
    /// <summary>
    /// True when MAUI bootstrap succeeded and <see cref="Application.Current"/> exists.
    /// Does not create a <see cref="Window"/> — use <see cref="EnsurePrimaryWindowAsync"/> for that.
    /// </summary>
    public static bool CanUseVisualTree =>
        MauiUiTestBootstrap.IsReady && Application.Current is not null;

    /// <summary>
    /// Ensures <see cref="Application.Current"/> has at least one <see cref="Window"/> for visual-tree tests.
    /// On Android/iOS device hosts, <see cref="Application.Windows"/> is often empty until a Window is opened explicitly.
    /// </summary>
    public static Task<bool> EnsurePrimaryWindowAsync()
    {
        if (Application.Current is null)
        {
            return Task.FromResult(false);
        }

        if (Application.Current.Windows.Count > 0)
        {
            return Task.FromResult(true);
        }

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if ((Application.Current?.Windows.Count ?? 0) == 0 && Application.Current is not null)
            {
                Application.Current.OpenWindow(new Window(new NavigationPage(new ContentPage())));
            }

            return Task.FromResult((Application.Current?.Windows.Count ?? 0) > 0);
        });
    }

    /// <summary>
    /// Drains posted <see cref="MainThread"/> callbacks so assertions run after BeginInvokeOnMainThread work.
    /// Safe to call from either the MAUI main thread or a background test thread.
    /// </summary>
    public static async Task FlushMainThreadAsync()
    {
        if (MainThread.IsMainThread)
        {
            await Task.Yield();
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () => await Task.Yield());
    }
}
