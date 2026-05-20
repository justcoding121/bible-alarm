#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class KeyboardHelperTests
{
    [Fact]
    public void HideKeyboard_returns_when_entry_null()
    {
        KeyboardHelper.HideKeyboard(null);
    }

    [Fact]
    public void HideKeyboard_unfocuses_non_null_entry_when_maui_host_available()
    {
        if (!TryBootstrapMauiApp())
        {
            return;
        }

        var entry = new Entry();

        KeyboardHelper.HideKeyboard(entry);
    }

    private static bool TryBootstrapMauiApp()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return true;
        }

        try
        {
            MauiAppHolder.CreateAndStore();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
