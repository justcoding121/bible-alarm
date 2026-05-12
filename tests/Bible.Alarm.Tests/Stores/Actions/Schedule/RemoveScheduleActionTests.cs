#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class RemoveScheduleActionTests
{
    [Fact]
    public void Constructor_sets_Schedule()
    {
        var schedule = new AlarmSchedule { Id = 22 };

        var sut = new RemoveScheduleAction(schedule);

        Assert.Same(schedule, sut.Schedule);
    }
}
