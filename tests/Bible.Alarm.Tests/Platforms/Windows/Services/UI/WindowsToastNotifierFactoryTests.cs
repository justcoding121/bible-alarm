#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsToastNotifierFactoryTests
{
    [Fact]
    public void GetToastNotifier_does_not_throw_on_test_host()
    {
        var ex = Record.Exception(() => WindowsToastNotifierFactory.GetToastNotifier());

        Assert.Null(ex);
    }
}
