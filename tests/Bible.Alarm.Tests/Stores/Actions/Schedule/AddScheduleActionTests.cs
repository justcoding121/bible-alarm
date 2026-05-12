#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class AddScheduleActionTests
{
    [Fact]
    public void Constructor_sets_Schedule()
    {
        var schedule = new AlarmSchedule { Id = 42 };

        var sut = new AddScheduleAction(schedule);

        Assert.Same(schedule, sut.Schedule);
    }
}
