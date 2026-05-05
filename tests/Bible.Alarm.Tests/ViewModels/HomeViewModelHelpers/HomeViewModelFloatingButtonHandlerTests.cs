#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelFloatingButtonHandlerTests
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void ComputeFloatingButtonVisible_follows_platform_rules()
    {
        var logger = TestLogging.CreateLogger();
        var sut = new HomeViewModelFloatingButtonHandler(logger, new EmptyServiceProvider());

        var visible = sut.ComputeFloatingButtonVisible();

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            // Android: handler may show the battery-permission nudge when optimization service isn't wired.
            Assert.True(visible);
        }
        else
        {
            Assert.False(visible);
        }
    }
}
