#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

namespace Bible.Alarm.Tests;

public sealed class SectionListLoaderTests
{
    private sealed class StubMedia : IMediaService
    {
        public SortedDictionary<string, BiblePublicationSection> SectionsResult { get; init; } = [];

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
            Task.FromResult(SectionsResult);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

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
            Task.FromResult(0);
    }

    [Fact]
    public async Task LoadAsync_English_ReturnsEmpty_When_NoSections()
    {
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = [] }, internetChecker: null);

        var (items, map) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: null);

        Assert.Empty(items);
        Assert.Empty(map);
    }

    [Fact]
    public async Task LoadAsync_English_ReturnsSections_When_Cataloged()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new BiblePublicationSection { Id = 50, SectionCode = "1", Name = "Genesis" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, map) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: "1");

        Assert.Single(items);
        Assert.Single(map);
        Assert.True(items[0].IsSelected);
    }

    [Fact]
    public async Task LoadAsync_removes_placeholder_section_with_zero_id()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new BiblePublicationSection { Id = 0, SectionCode = "1", Name = "Placeholder" },
            ["2"] = new BiblePublicationSection { Id = 10, SectionCode = "2", Name = "Genesis" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, map) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: null);

        Assert.Single(items);
        Assert.Equal("2", items[0].SectionCode);
    }

    [Fact]
    public async Task LoadAsync_removes_placeholder_section_with_empty_name()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new BiblePublicationSection { Id = 5, SectionCode = "1", Name = "" },
            ["2"] = new BiblePublicationSection { Id = 10, SectionCode = "2", Name = "Ruth" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, map) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: null);

        Assert.Single(items);
        Assert.Equal("2", items[0].SectionCode);
    }

    [Fact]
    public async Task LoadAsync_returns_empty_when_all_sections_are_placeholders()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new BiblePublicationSection { Id = 0, SectionCode = "1", Name = "Placeholder" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, map) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: null);

        Assert.Empty(items);
        Assert.Empty(map);
    }

    [Fact]
    public async Task LoadAsync_no_section_selected_when_selectedSectionCode_does_not_match()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new BiblePublicationSection { Id = 5, SectionCode = "1", Name = "Genesis" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, _) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: "999");

        Assert.Single(items);
        Assert.False(items[0].IsSelected);
    }

    [Fact]
    public async Task LoadAsync_section_selected_case_insensitively()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["ABC"] = new BiblePublicationSection { Id = 7, SectionCode = "ABC", Name = "Section ABC" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, _) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: "abc");

        Assert.Single(items);
        Assert.True(items[0].IsSelected);
    }

    [Fact]
    public async Task LoadAsync_map_is_keyed_by_section_code()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["3"] = new BiblePublicationSection { Id = 3, SectionCode = "3", Name = "Leviticus" },
            ["1"] = new BiblePublicationSection { Id = 1, SectionCode = "1", Name = "Genesis" },
            ["2"] = new BiblePublicationSection { Id = 2, SectionCode = "2", Name = "Exodus" },
        };
        var sut = new SectionListLoader(TestLogging.CreateLogger(), new StubMedia { SectionsResult = sections }, internetChecker: null);

        var (items, map) = await sut.LoadAsync("E", "nwtsty", selectedSectionCode: null);

        Assert.Equal(3, items.Count);
        Assert.True(map.ContainsKey("1"));
        Assert.True(map.ContainsKey("2"));
        Assert.True(map.ContainsKey("3"));
    }
}
