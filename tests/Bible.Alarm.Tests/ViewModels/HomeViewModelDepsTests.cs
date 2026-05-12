#nullable enable

using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new HomeViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.Mapper);
    }
}
