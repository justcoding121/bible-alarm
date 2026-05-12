#nullable enable

using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

namespace Bible.Alarm.Tests;

public sealed class CategorySelectionAutoPopulateHandlerDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new CategorySelectionAutoPopulateHandlerDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        Assert.Null(sut.BiblePublicationService);
        Assert.Null(sut.Logger);
    }
}
