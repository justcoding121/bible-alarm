#nullable enable

using Bible.Alarm.Common;

namespace Bible.Alarm.Tests;

public sealed class MauiAppHolderTests
{
    [Fact]
    public void App_getter_throws_when_app_was_never_created()
    {
        if (MauiAppHolder.IsInitialized)
        {
            // Android/iOS device-test hosts (and some integration paths) boot the real MAUI
            // application before xunit runs, so the holder is never empty here.
            return;
        }

        var ex = Assert.Throws<InvalidOperationException>(() => _ = MauiAppHolder.App);

        Assert.Contains("Call CreateAndStore()", ex.Message, StringComparison.Ordinal);
    }
}
