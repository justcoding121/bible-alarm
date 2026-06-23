#nullable enable

using Bible.Alarm.Common.ViewHelpers.Converters;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;

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
    /// Merges Styles and seeds StaticResource/DynamicResource keys required by <see cref="Views.Home"/>
    /// and <see cref="Views.Shared.MiniPlaybackBar"/> on device hosts where the minimal test
    /// <see cref="Application"/> has no <c>App.xaml</c> resources.
    /// </summary>
    public static void EnsureAppResources()
    {
        if (Application.Current is null)
        {
            return;
        }

        var resources = Application.Current.Resources;
        if (resources.ContainsKey("MauiUiTestResourcesSeeded"))
        {
            return;
        }

        resources.MergedDictionaries.Add(new Styles());

        resources["PrimaryColor"] = Color.FromArgb("#6A5ACD");
        resources["PrimaryLightColor"] = Color.FromArgb("#9370DB");
        resources["ErrorColor"] = Color.FromArgb("#FF3B30");
        resources["WarningColor"] = Color.FromArgb("#FF9500");
        resources["SuccessColor"] = Color.FromArgb("#34C759");

        resources["negateBooleanConverter"] = new NegateBooleanConverter();
        resources["dayColorConverter"] = new DayColorConverter();
        resources["dayBackgroundColorConverter"] = new DayBackgroundColorConverter();
        resources["dayOpacityConverter"] = new DayOpacityConverter();
        resources["isEnabledColorConverter"] = new IsEnabledColorConverter();
        resources["itemTappedConverter"] = new ItemTappedEventArgsConverter();
        resources["multiBoolOrConverter"] = new MultiBoolOrConverter();
        resources["isStringNotEmptyConverter"] = new IsStringNotEmptyConverter();
        resources["booleanToOpacityConverter"] = new BoolToOpacityConverter();
        resources["doubleToBottomThicknessConverter"] = new DoubleToBottomThicknessConverter();

        resources["PageBackgroundColor"] = Colors.White;
        resources["CardBackgroundColor"] = Colors.White;
        resources["BackgroundColor"] = Colors.White;
        resources["TextPrimaryColor"] = Colors.Black;
        resources["PrimaryTextColor"] = Colors.Black;
        resources["TextSecondaryColor"] = Colors.Gray;
        resources["ControlBackgroundColor"] = Colors.LightGray;
        resources["ProgressBarBackgroundColor"] = Colors.LightGray;
        resources["DisabledTextColor"] = Colors.Gray;
        resources["DividerColor"] = Colors.LightGray;
        resources["SelectedItemBackgroundColor"] = Colors.LightBlue;

        resources["MauiUiTestResourcesSeeded"] = true;
    }

    /// <summary>
    /// Ensures <see cref="Application.Current"/> has at least one <see cref="Window"/> for visual-tree tests.
    /// On Android/iOS device hosts, <see cref="Application.Windows"/> is often empty until a Window is opened explicitly.
    /// Returns false when no window could be opened within <see cref="PrimaryWindowTimeout"/>.
    /// </summary>
    public static async Task<bool> EnsurePrimaryWindowAsync()
    {
        if (Application.Current is null)
        {
            return false;
        }

        if (Application.Current.Windows.Count > 0)
        {
            return true;
        }

        var openWindow = MainThread.InvokeOnMainThreadAsync(() =>
        {
            if ((Application.Current?.Windows.Count ?? 0) == 0 && Application.Current is not null)
            {
                Application.Current.OpenWindow(new Window(new NavigationPage(new ContentPage())));
            }

            return Task.FromResult((Application.Current?.Windows.Count ?? 0) > 0);
        });

        var completed = await Task.WhenAny(openWindow, Task.Delay(PrimaryWindowTimeout));
        if (completed != openWindow)
        {
            return false;
        }

        return await openWindow;
    }

    /// <summary>
    /// Runs work on the MAUI main thread when the host is ready. Returns false when the main thread
    /// does not respond within <see cref="PrimaryWindowTimeout"/> (common on slow CI emulators).
    /// </summary>
    public static async Task<bool> TryInvokeOnMainThreadAsync(Func<Task> work)
    {
        if (!CanUseVisualTree)
        {
            return false;
        }

        if (MainThread.IsMainThread)
        {
            await work();
            return true;
        }

        var invoke = MainThread.InvokeOnMainThreadAsync(work);
        var completed = await Task.WhenAny(invoke, Task.Delay(PrimaryWindowTimeout));
        if (completed != invoke)
        {
            return false;
        }

        await invoke;
        return true;
    }

    /// <summary>
    /// Runs synchronous work on the MAUI main thread when the host is ready.
    /// </summary>
    public static Task<bool> TryInvokeOnMainThreadAsync(Action work) =>
        TryInvokeOnMainThreadAsync(() =>
        {
            work();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Creates a <see cref="Home"/> page when Syncfusion handlers are available on the test host.
    /// Returns null on iOS simulator CI where linker/bootstrap may not register <c>SfEffectsView</c>.
    /// </summary>
    public static Home? TryCreateHomePage(HomeViewModel vm)
    {
        try
        {
            return new Home(vm);
        }
        catch (XamlParseException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a <see cref="Home"/> page on the MAUI main thread when possible.
    /// </summary>
    public static async Task<Home?> TryCreateHomePageAsync(HomeViewModel vm)
    {
        Home? home = null;
        if (!await TryInvokeOnMainThreadAsync(() => home = TryCreateHomePage(vm)))
        {
            return null;
        }

        return home;
    }

    private static readonly TimeSpan PrimaryWindowTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Drains posted <see cref="MainThread"/> callbacks so assertions run after BeginInvokeOnMainThread work.
    /// Safe to call from either the MAUI main thread or a background test thread.
    /// Returns false when the main thread did not drain within <see cref="PrimaryWindowTimeout"/>.
    /// </summary>
    public static async Task<bool> FlushMainThreadAsync()
    {
        if (MainThread.IsMainThread)
        {
            await Task.Yield();
            return true;
        }

        var flush = MainThread.InvokeOnMainThreadAsync(async () => await Task.Yield());
        var completed = await Task.WhenAny(flush, Task.Delay(PrimaryWindowTimeout));
        if (completed != flush)
        {
            return false;
        }

        await flush;
        return true;
    }
}
