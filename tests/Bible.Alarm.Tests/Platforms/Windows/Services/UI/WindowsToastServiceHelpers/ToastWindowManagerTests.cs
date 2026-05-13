#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class ToastWindowManagerTests
{
    [Fact]
    public void GetNativeWindow_does_not_throw_when_host_is_uninitialized()
    {
        var ex = Record.Exception(() => _ = ToastWindowManager.GetNativeWindow());

        Assert.Null(ex);
    }
}
