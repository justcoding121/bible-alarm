#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class PlaylistMusicTrackBuilderTests
{
    private sealed class StubUrlConstruction : IUrlConstructionService
    {
        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) =>
            Task.FromResult<List<string>>([]);

        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<List<string>>([]);

        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<string?>("/lookup");

        public void ClearLookUpPathCache()
        {
        }
    }

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult<string?>("https://music.example/track.mp3");
    }

    private sealed class StubMediaService : IMediaService
    {
        public bool NoLanguagePublication { get; init; }

        public SortedDictionary<int, MusicTrack>? VocalTracks { get; init; }

        public SortedDictionary<int, MusicTrack>? MelodyTracks { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null) =>
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
            Task.FromResult(MelodyTracks ?? new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(VocalTracks ?? new SortedDictionary<int, MusicTrack>());

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
            Task.FromResult(NoLanguagePublication);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    [Fact]
    public void Constructor_throws_when_url_construction_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistMusicTrackBuilder(new StubMediaService(), new StubUrlRefresh(), null!));
    }

    [Fact]
    public async Task NextMusicUrlToPlay_VocalPublication_BuildsPlayItem()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "1", Title = "Song A" },
            [2] = new MusicTrack { TrackCode = "2", Title = "Song B" },
        };

        var media = new StubMediaService { NoLanguagePublication = false, VocalTracks = tracks };
        var sut = new PlaylistMusicTrackBuilder(media, new StubUrlRefresh(), new StubUrlConstruction());

        var schedule = new AlarmSchedule
        {
            Id = 11,
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "vocpub",
                TrackCode = "1",
            },
        };

        var item = await sut.NextMusicUrlToPlay(schedule, next: false);

        Assert.Equal(11, item.Metadata.ScheduleId);
        Assert.Equal("vocpub", item.Metadata.PublicationCode);
        Assert.Equal("E", item.Metadata.LanguageCode);
        Assert.Equal("1", item.Metadata.TrackCode);
        Assert.Equal("https://music.example/track.mp3", item.Url);
    }

    [Fact]
    public async Task NextMusicUrlToPlay_VocalPublication_wraps_to_first_track()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "1", Title = "Song A" },
            [2] = new MusicTrack { TrackCode = "2", Title = "Song B" },
        };
        var sut = new PlaylistMusicTrackBuilder(
            new StubMediaService { VocalTracks = tracks },
            new StubUrlRefresh(),
            new StubUrlConstruction());
        var schedule = new AlarmSchedule
        {
            Id = 3,
            Music = new AlarmMusic { LanguageCode = "E", PublicationCode = "sjjc", TrackCode = "2" },
        };

        var item = await sut.NextMusicUrlToPlay(schedule, next: true);

        Assert.Equal("1", item.Metadata.TrackCode);
    }

    [Fact]
    public async Task PreviousMusicUrlToPlay_VocalPublication_wraps_to_last_track()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "1", Title = "Song A" },
            [2] = new MusicTrack { TrackCode = "2", Title = "Song B" },
        };
        var sut = new PlaylistMusicTrackBuilder(
            new StubMediaService { VocalTracks = tracks },
            new StubUrlRefresh(),
            new StubUrlConstruction());
        var schedule = new AlarmSchedule
        {
            Id = 3,
            Music = new AlarmMusic { LanguageCode = "E", PublicationCode = "sjjc", TrackCode = "1" },
        };

        var item = await sut.PreviousMusicUrlToPlay(schedule);

        Assert.Equal("2", item.Metadata.TrackCode);
    }

    [Fact]
    public async Task NextMusicUrlToPlay_VocalPublication_throws_when_track_list_empty()
    {
        var sut = new PlaylistMusicTrackBuilder(
            new StubMediaService { VocalTracks = [] },
            new StubUrlRefresh(),
            new StubUrlConstruction());
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic { LanguageCode = "E", PublicationCode = "sjjc", TrackCode = "1" },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.NextMusicUrlToPlay(schedule, next: true));
    }

    [Fact]
    public async Task NextMusicUrlToPlay_MelodyPublication_uses_no_language_path()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "10", Title = "Melody A", DownloadCode = "iam-1" },
            [2] = new MusicTrack { TrackCode = "11", Title = "Melody B", DownloadCode = "iam-1" },
        };
        var sut = new PlaylistMusicTrackBuilder(
            new StubMediaService { NoLanguagePublication = true, MelodyTracks = tracks },
            new StubUrlRefresh(),
            new StubUrlConstruction());
        var schedule = new AlarmSchedule
        {
            Id = 8,
            Music = new AlarmMusic { PublicationCode = "osg", TrackCode = "10" },
        };

        var item = await sut.NextMusicUrlToPlay(schedule, next: true);

        Assert.Equal("11", item.Metadata.TrackCode);
        Assert.Equal("osg", item.Metadata.PublicationCode);
        Assert.Equal(string.Empty, item.Metadata.LanguageCode);
    }
}
