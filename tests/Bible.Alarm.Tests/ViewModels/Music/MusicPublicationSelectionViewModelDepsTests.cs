#nullable enable

using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionViewModelDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new MusicPublicationSelectionViewModelDeps(
            null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.MediaService);
        Assert.Null(sut.ServiceProvider);
    }
}
