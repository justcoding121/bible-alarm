#nullable enable

using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class VocalMusicFirstPublicationTrackSelectorTests
{
    [Fact]
    public void Ctor_accepts_service_slots()
    {
        var sut = new VocalMusicFirstPublicationTrackSelector(
            null!,
            biblePublicationService: null,
            languageContentService: null,
            scopeFactory: null);
        Assert.NotNull(sut);
    }
}
