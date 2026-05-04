#nullable enable

using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

[Collection(nameof(MutableNavigationContextCollection))]
public sealed class ScheduleNavigationContextTests
{
    public ScheduleNavigationContextTests()
    {
        ScheduleNavigationContext.Clear();
    }

    [Fact]
    public void Roundtrip_then_clear_resets_static_properties()
    {
        ScheduleNavigationContext.ScheduleIdToLoad = 42;
        ScheduleNavigationContext.IsEnabledToLoad = true;

        Assert.Equal(42, ScheduleNavigationContext.ScheduleIdToLoad);
        Assert.True(ScheduleNavigationContext.IsEnabledToLoad);

        ScheduleNavigationContext.Clear();

        Assert.Null(ScheduleNavigationContext.ScheduleIdToLoad);
        Assert.False(ScheduleNavigationContext.IsEnabledToLoad);
    }
}
