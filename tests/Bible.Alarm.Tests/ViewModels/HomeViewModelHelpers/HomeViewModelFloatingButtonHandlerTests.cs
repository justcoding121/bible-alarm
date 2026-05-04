#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelFloatingButtonHandlerTests
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void ComputeFloatingButtonVisible_is_false_off_Android()
    {
        var logger = TestLogging.CreateLogger();
        var sut = new HomeViewModelFloatingButtonHandler(logger, new EmptyServiceProvider());

        Assert.False(sut.ComputeFloatingButtonVisible());
    }
}
