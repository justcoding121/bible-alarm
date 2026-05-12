#nullable enable

using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new PlaybackViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.AudioPlayer);
    }
}
