#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.General;

namespace Bible.Alarm.Tests;

public sealed class BatteryOptimizationViewModelTests
{
    [Fact]
    public void Ctor_initializes_on_non_android_without_permission_probe()
    {
        var sut = new BatteryOptimizationViewModel(
            TestLogging.CreateLogger(),
            null!,
            null!);
        Assert.NotNull(sut);
    }
}
