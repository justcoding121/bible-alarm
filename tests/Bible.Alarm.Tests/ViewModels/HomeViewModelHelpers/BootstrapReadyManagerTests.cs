#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class BootstrapReadyManagerTests
{
    [Fact]
    public void Dispose_can_be_called_twice_without_throwing()
    {
        var sut = new BootstrapReadyManager(TestLogging.CreateLogger());

        sut.Dispose();
        sut.Dispose();
    }
}
