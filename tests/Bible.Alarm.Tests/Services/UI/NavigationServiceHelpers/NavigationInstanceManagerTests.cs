#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class NavigationInstanceManagerTests
{
    [Fact]
    public void Ctor_accepts_logger()
    {
        var sut = new NavigationInstanceManager(TestLogging.CreateLogger());
        Assert.NotNull(sut);
    }
}
