#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionDataProviderTests
{
    private sealed class VocalTracksMediaStub : IMediaService
    {
        internal SortedDictionary<int, MusicTrack>? VocalTracksResult { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

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
            Task.FromResult(VocalTracksResult ?? new SortedDictionary<int, MusicTrack>());

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

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private static PublicationListViewItemModel SongPublicationVm(string code = "osg") =>
        new(new BiblePublication
        {
            Id = 1,
            PublicationCode = code,
            Name = "Song pub",
            Sections = [],
            Tracks = [],
        });

    [Fact]
    public async Task GetTrackForSongPublicationAsync_returns_empty_when_no_tracks()
    {
        var media = new VocalTracksMediaStub { VocalTracksResult = new SortedDictionary<int, MusicTrack>() };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm(), "E", currentSchedule: null);

        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.TrackName);
    }

    [Fact]
    public async Task GetTrackForSongPublicationAsync_returns_only_track_when_schedule_differs()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "44", Title = "Song forty-four", Url = "", LookUpPath = "" },
        };

        var media = new VocalTracksMediaStub { VocalTracksResult = tracks };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm(), "E", currentSchedule: null);

        Assert.Equal("44", result.TrackCode);
        Assert.Equal("Song forty-four", result.TrackName);
    }

    [Fact]
    public async Task GetTrackForSongPublicationAsync_prefers_persisted_track_when_same_publication()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "5", Title = "Five", Url = "", LookUpPath = "" },
            [1] = new MusicTrack { TrackCode = "9", Title = "Nine", Url = "", LookUpPath = "" },
        };

        var media = new VocalTracksMediaStub { VocalTracksResult = tracks };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());

        var schedule = new ScheduleStateItem
        {
            Id = 1,
            Name = "Alarm",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicLanguageCode = "E",
            MusicPublicationCode = "osg",
            MusicTrackCode = "9",
        };

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm("osg"), "E", schedule);

        Assert.Equal("9", result.TrackCode);
        Assert.Equal("Nine", result.TrackName);
    }
}
