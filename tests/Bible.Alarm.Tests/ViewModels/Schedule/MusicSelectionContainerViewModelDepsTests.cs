#nullable enable

using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Tests;

public sealed class MusicSelectionContainerViewModelDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new MusicSelectionContainerViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.ToastService);
    }
}
