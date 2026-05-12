#nullable enable

using Bible.Alarm.Services.Scheduler;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateServiceDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new ScheduleStateServiceDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.ServiceProvider);
    }
}
