#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;
using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionResolverTests
{
    private sealed class OverrideTracksMedia(SortedDictionary<string, BiblePublicationTrack> tracks) : IMediaService
    {
        private readonly IdleCatalogMediaService inner = new();

        public void Dispose() => inner.Dispose();

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(tracks);

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            inner.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            inner.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            inner.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            inner.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => inner.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) => inner.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            inner.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => inner.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            inner.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            inner.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            inner.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            inner.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            inner.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) => inner.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            inner.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) => inner.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            inner.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            inner.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            inner.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }

    [Fact]
    public async Task BuildSelectionAsync_returns_null_when_section_code_blank()
    {
        var media = new OverrideTracksMedia(new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer));
        var sut = new TrackSelectionResolver(TestLogging.CreateLogger(), media);
        var section = new BiblePublicationSection { SectionCode = "  ", Name = "X" };
        var vm = new BiblePublicationSectionListViewItemModel(section);
        var schedule = new ScheduleStateItem { BiblePublicationCode = "nwt", BiblePublicationLanguageCode = "E" };

        var result = await sut.BuildSelectionAsync(vm, schedule, () => null);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSelectionAsync_returns_null_when_tracks_collection_empty()
    {
        var media = new OverrideTracksMedia(new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer));
        var sut = new TrackSelectionResolver(TestLogging.CreateLogger(), media);
        var section = new BiblePublicationSection { SectionCode = "40", Name = "Genesis" };
        var vm = new BiblePublicationSectionListViewItemModel(section);
        var schedule = new ScheduleStateItem { BiblePublicationCode = "nwt", BiblePublicationLanguageCode = "E" };

        var result = await sut.BuildSelectionAsync(vm, schedule, () => null);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSelectionAsync_preserves_existing_track_when_section_unchanged()
    {
        var tracks = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer)
        {
            ["3"] = new BiblePublicationTrack { TrackCode = "3", Title = "Three" },
            ["4"] = new BiblePublicationTrack { TrackCode = "4", Title = "Four" },
        };
        var media = new OverrideTracksMedia(tracks);
        var sut = new TrackSelectionResolver(TestLogging.CreateLogger(), media);
        var section = new BiblePublicationSection { SectionCode = "40", Name = "Genesis" };
        var vm = new BiblePublicationSectionListViewItemModel(section);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCode = "nwt",
            BiblePublicationLanguageCode = "E",
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "3",
        };

        var result = await sut.BuildSelectionAsync(vm, schedule, () => null);

        Assert.NotNull(result);
        Assert.Equal("3", result!.TrackCode);
        Assert.Equal("Three", result.TrackTitle);
    }

    [Fact]
    public async Task BuildSelectionAsync_picks_first_track_when_section_changes()
    {
        var tracks = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer)
        {
            ["9"] = new BiblePublicationTrack { TrackCode = "9", Title = "Nine" },
            ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "Two" },
        };
        var media = new OverrideTracksMedia(tracks);
        var sut = new TrackSelectionResolver(TestLogging.CreateLogger(), media);
        var section = new BiblePublicationSection { SectionCode = "50", Name = "Exodus" };
        var vm = new BiblePublicationSectionListViewItemModel(section);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCode = "nwt",
            BiblePublicationLanguageCode = "E",
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "9",
        };

        var result = await sut.BuildSelectionAsync(vm, schedule, () => null);

        Assert.NotNull(result);
        Assert.Equal("2", result!.TrackCode);
        Assert.Equal("Two", result.TrackTitle);
    }
}
