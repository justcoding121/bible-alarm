#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicInstrumentalSectionListLoaderTests
{
    private sealed class StubMedia : IMediaService
    {
        public SortedDictionary<string, BiblePublicationSection> Sections { get; init; } = [];
        public int ExpectedSectionCount { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(Sections);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(ExpectedSectionCount);
    }

    [Fact]
    public async Task LoadAsync_WhenFullyCataloged_BuildsItemsAndSelection()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["disc-a"] = new BiblePublicationSection
            {
                Id = 10,
                SectionCode = "disc-a",
                Name = "Disc A",
            },
        };

        var media = new StubMedia { Sections = sections, ExpectedSectionCount = 1 };
        var sut = new MusicInstrumentalSectionListLoader(TestLogging.CreateLogger(), media);

        var (items, selected) = await sut.LoadAsync("iam-1", selectedSectionCode: "disc-a");

        var single = Assert.Single(items);
        Assert.Equal("disc-a", single.SectionCode);
        Assert.NotNull(selected);
        Assert.True(selected!.IsSelected);
    }

    [Fact]
    public async Task LoadAsync_WhenFullyCataloged_OrdersSectionsByDisplayComparer()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["zebra"] = new BiblePublicationSection { Id = 2, SectionCode = "zebra", Name = "Z" },
            ["alpha"] = new BiblePublicationSection { Id = 3, SectionCode = "alpha", Name = "A" },
        };

        var media = new StubMedia { Sections = sections, ExpectedSectionCount = 2 };
        var sut = new MusicInstrumentalSectionListLoader(TestLogging.CreateLogger(), media);

        var (items, _) = await sut.LoadAsync("iam-1", selectedSectionCode: null);

        Assert.Equal(2, items.Count);
        var alphaIdx = items.FindIndex(i => i.SectionCode.Equals("alpha", StringComparison.OrdinalIgnoreCase));
        var zebraIdx = items.FindIndex(i => i.SectionCode.Equals("zebra", StringComparison.OrdinalIgnoreCase));
        Assert.True(alphaIdx >= 0 && zebraIdx >= 0 && alphaIdx < zebraIdx);
    }
}
