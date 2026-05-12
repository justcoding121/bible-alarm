#nullable enable

using Bible.Alarm.Services.Bootstrap;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStatePopulatorDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new ScheduleStatePopulatorDeps(
            null,
            null,
            null!,
            null,
            null,
            null,
            null!);

        Assert.Null(sut.BiblePublicationService);
        Assert.Null(sut.Mapper);
        Assert.Null(sut.ScopeFactory);
    }
}
