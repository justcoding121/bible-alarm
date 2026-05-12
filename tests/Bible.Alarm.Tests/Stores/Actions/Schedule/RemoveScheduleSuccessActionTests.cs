#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class RemoveScheduleSuccessActionTests
{
    [Fact]
    public void Constructor_sets_ScheduleId()
    {
        var sut = new RemoveScheduleSuccessAction(21);

        Assert.Equal(21, sut.ScheduleId);
    }
}
