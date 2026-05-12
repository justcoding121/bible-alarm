#nullable enable

using Bible.Alarm.ViewModels.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionViewModelDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots_and_optional_default()
    {
        var sut = new BiblePublicationSelectionViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            BiblePublicationService: null);

        Assert.Null(sut.MediaService);
        Assert.Null(sut.BiblePublicationService);
    }
}
