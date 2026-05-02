#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class LookupDataLoaderTests
{
    private static LookupDataCollector.LookupKeys EmptyKeys() =>
        new(
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance),
            new HashSet<(string LanguageCode, string PublicationCode, string SectionCode)>(PublicationLookupKeyComparers.LanguagePublicationSection.Instance),
            new HashSet<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)>(
                PublicationLookupKeyComparers.LanguagePublicationNullableSectionTrack.Instance),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance),
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<(string PublicationCode, string SectionCode)>(PublicationLookupKeyComparers.PublicationSection.Instance));

    [Fact]
    public async Task LoadAllAsync_WithEmptyKeys_ReturnsEmptyLookupTables()
    {
        var sut = new LookupDataLoader(
            BiblePublicationService: null,
            biblePublicationSectionService: null,
            mediaService: null,
            vocalMusicService: null,
            scopeFactory: null);

        var data = await sut.LoadAllAsync(EmptyKeys());

        Assert.Empty(data.Publications);
        Assert.Empty(data.Sections);
        Assert.Empty(data.NoLanguagePublications);
        Assert.Empty(data.NoLanguageSections);
        Assert.Empty(data.NoLanguageTrackTitles);
        Assert.Empty(data.VocalLanguages);
        Assert.Empty(data.VocalReleases);
        Assert.Empty(data.VocalTracks);
        Assert.Empty(data.MelodyTracksFlat);
        Assert.Empty(data.MelodyTracksBySection);
        Assert.Empty(data.MelodyReleases);
    }
}
