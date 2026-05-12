#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ViewExistingScheduleActionTests
{
    [Fact]
    public void Constructor_sets_ScheduleId_and_IsEnabled()
    {
        var sut = new ViewExistingScheduleAction(200, isEnabled: false);

        Assert.Equal(200, sut.ScheduleId);
        Assert.False(sut.IsEnabled);
    }
}
