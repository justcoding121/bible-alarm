#nullable enable

using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

namespace Bible.Alarm.Tests;

public sealed class MusicStateChangeHandlerCtorDepsTests
{
    [Fact]
    public void MusicStateChangeHandlerServices_round_trips_dependency_slots()
    {
        var sut = new MusicStateChangeHandlerServices(
            null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.ServiceProvider);
    }

    [Fact]
    public void MusicStateChangeHandlerCollaborators_round_trips_dependency_slots()
    {
        var sut = new MusicStateChangeHandlerCollaborators(null!, null!, null!);

        Assert.Null(sut.StateTracker);
        Assert.Null(sut.DisplayTextProvider);
    }
}
