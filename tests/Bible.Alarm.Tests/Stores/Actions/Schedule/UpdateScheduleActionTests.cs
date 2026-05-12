#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class UpdateScheduleActionTests
{
    [Fact]
    public void Constructor_sets_Schedule()
    {
        var schedule = new AlarmSchedule { Id = 15 };

        var sut = new UpdateScheduleAction(schedule);

        Assert.Same(schedule, sut.Schedule);
    }
}
