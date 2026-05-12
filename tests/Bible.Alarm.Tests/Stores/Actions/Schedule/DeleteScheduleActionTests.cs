#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class DeleteScheduleActionTests
{
    [Fact]
    public void Record_holds_ScheduleId()
    {
        var sut = new DeleteScheduleAction(404);

        Assert.Equal(404, sut.ScheduleId);
    }
}
