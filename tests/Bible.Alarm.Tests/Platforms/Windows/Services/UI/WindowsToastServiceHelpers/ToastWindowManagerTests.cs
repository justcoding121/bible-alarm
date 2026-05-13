#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class ToastWindowManagerTests
{
    [Fact]
    public void GetNativeWindow_returns_null_when_maui_application_has_no_windows()
    {
        Assert.Null(ToastWindowManager.GetNativeWindow());
    }
}
