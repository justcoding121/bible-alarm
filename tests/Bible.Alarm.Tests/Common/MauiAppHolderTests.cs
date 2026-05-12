#nullable enable

using Bible.Alarm.Common;

namespace Bible.Alarm.Tests;

public sealed class MauiAppHolderTests
{
    [Fact]
    public void App_getter_throws_when_app_was_never_created()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _ = MauiAppHolder.App);

        Assert.Contains("Call CreateAndStore()", ex.Message, StringComparison.Ordinal);
    }
}
