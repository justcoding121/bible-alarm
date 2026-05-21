#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class KeyboardHelperTests(MauiUiFixture fixture)
{

    [Fact]
    public void HideKeyboard_returns_when_entry_null()
    {
        _ = fixture;
        KeyboardHelper.HideKeyboard(null);
    }

    [Fact]
    public void HideKeyboard_unfocuses_non_null_entry_when_maui_host_available()
    {
        if (!TryBootstrapMauiApp())
        {
            return;
        }

        var done = new ManualResetEventSlim(false);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var entry = new Entry();
            entry.Focus();
            KeyboardHelper.HideKeyboard(entry);
            done.Set();
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
    }

    private static bool TryBootstrapMauiApp()
    {
        MauiUiTestBootstrap.TryInitialize();
        return MauiUiTestBootstrap.IsReady;
    }
}
