#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class HomeNavigationHandlerTests
{
    [Fact]
    public void Ctor_accepts_logger_and_service_provider()
    {
        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), null!);
        Assert.NotNull(sut);
    }
}
