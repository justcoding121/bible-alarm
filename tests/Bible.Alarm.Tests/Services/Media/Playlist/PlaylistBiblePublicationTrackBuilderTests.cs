#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class PlaylistBiblePublicationTrackBuilderTests
{
    private sealed class StubUrlConstruction : IUrlConstructionService
    {
        public string PathToReturn { get; init; } = "/lookup";

        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) =>
            Task.FromResult<List<string>>([]);

        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<List<string>>([]);

        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<string?>(PathToReturn);

        public void ClearLookUpPathCache()
        {
        }
    }

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public string UrlToReturn { get; init; } = "https://example.test/bible.mp3";

        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult<string?>(UrlToReturn);
    }

    private sealed class StubMediaService : IMediaService
    {
        public SortedDictionary<string, BiblePublicationTrack>? TracksToReturn { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(TracksToReturn ?? new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer));

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
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

    private sealed class NullPathUrlConstruction : IUrlConstructionService
    {
        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) => Task.FromResult<List<string>>([]);
        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode, string? sectionCode, string trackCode) => Task.FromResult<List<string>>([]);
        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode, string? sectionCode, string trackCode) => Task.FromResult<string?>(null);
        public void ClearLookUpPathCache() { }
    }

    private sealed class NullUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) => Task.FromResult<string?>(null);
    }

    private static SortedDictionary<string, BiblePublicationTrack> SingleTrackDict(string trackCode) =>
        new(TrackCodeComparer.Comparer)
        {
            [trackCode] = new BiblePublicationTrack { TrackCode = trackCode, Title = "Chapter" },
        };

    [Fact]
    public async Task GetInitialTrackInfo_SectionedPublication_ResolvesTrackUrl()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var br = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "1",
            TrackCode = "1",
        };

        var info = await sut.GetInitialTrackInfo(br);

        Assert.Equal("nwtsty", info.PublicationCode);
        Assert.Equal("1", info.SectionCode);
        Assert.Equal("https://example.test/bible.mp3", info.Url);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_SingleTrack_DoesNotInvokeNavigationCallback()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("2") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "2",
        };

        var alarmSchedule = new AlarmSchedule
        {
            NumberOfTracksToPlay = 1,
        };

        var navigations = 0;
        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 9,
            alarmSchedule,
            biblePublicationSchedule,
            (_, _, _, _) =>
            {
                navigations++;
                return Task.FromResult(new TrackNavigationResult(
                    "x",
                    null,
                    new BiblePublicationTrack { TrackCode = "9" }));
            });

        Assert.Equal(0, navigations);
        var single = Assert.Single(items);
        Assert.Equal(9, single.Metadata.ScheduleId);
        Assert.True(single.Metadata.IsBibleContent);
        Assert.Equal("https://example.test/bible.mp3", single.Url);
    }

    [Fact]
    public async Task GetInitialTrackInfo_Sectioned_TrackNotFoundByCode_Throws()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("999") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var br = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "1",
            TrackCode = "1",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInitialTrackInfo(br));
    }

    [Fact]
    public async Task GetInitialTrackInfo_Sectioned_LookupPathEmpty_Throws()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new NullPathUrlConstruction(),
            biblePublicationService: null);

        var br = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "1",
            TrackCode = "1",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInitialTrackInfo(br));
    }

    [Fact]
    public async Task GetInitialTrackInfo_Sectioned_UrlEmpty_Throws()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new NullUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var br = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "1",
            TrackCode = "1",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInitialTrackInfo(br));
    }

    [Fact]
    public async Task GetInitialTrackInfo_NonSectioned_BiblePublicationServiceNull_Throws()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var br = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = null,
            TrackCode = "1",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInitialTrackInfo(br));
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_TwoTracksRequested_InvokesNavigationOnce()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
        };

        var navigations = 0;
        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = 2 },
            biblePublicationSchedule,
            (lang, pub, sec, track) =>
            {
                navigations++;
                return Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" }));
            });

        Assert.Equal(1, navigations);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_IndefiniteMode_ReturnsSingleTrack()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
        };

        var navigations = 0;
        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = 0 },
            biblePublicationSchedule,
            (lang, pub, sec, track) =>
            {
                navigations++;
                return Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" }));
            });

        Assert.Equal(0, navigations);
        Assert.Single(items);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_LastTrack_IsLastTrackSetTrue_InFiniteMode()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
        };

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = 1 },
            biblePublicationSchedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" })));

        var single = Assert.Single(items);
        Assert.True(single.Metadata.IsLastTrack);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_IndefiniteMode_IsLastTrackNeverSet()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
        };

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = 0 },
            biblePublicationSchedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" })));

        var single = Assert.Single(items);
        Assert.False(single.Metadata.IsLastTrack);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_FinishedDuration_AppliedToMatchingTrack()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
            FinishedDuration = TimeSpan.FromSeconds(30),
        };

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = 1, AlwaysPlayFromStart = false },
            biblePublicationSchedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" })));

        var single = Assert.Single(items);
        Assert.Equal(TimeSpan.FromSeconds(30), single.Metadata.FinishedDuration);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_AlwaysPlayFromStart_DoesNotApplyFinishedDuration()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
            FinishedDuration = TimeSpan.FromSeconds(30),
        };

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = 1, AlwaysPlayFromStart = true },
            biblePublicationSchedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" })));

        var single = Assert.Single(items);
        Assert.Equal(TimeSpan.Zero, single.Metadata.FinishedDuration);
    }

    [Fact]
    public async Task GetNextTrackInfo_ResolvesUrlForNextTrack()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("1") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh { UrlToReturn = "https://example.test/next.mp3" },
            new StubUrlConstruction(),
            biblePublicationService: null);

        var biblePublicationSchedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
        };

        var info = await sut.GetNextTrackInfo(
            biblePublicationSchedule,
            currentPublicationCode: "nwtsty",
            currentSectionCode: "40",
            currentTrackCode: "1",
            (lang, pub, sec, track) =>
                Task.FromResult(new TrackNavigationResult("nwtsty", null, new BiblePublicationTrack { TrackCode = "2" })));

        Assert.Equal("https://example.test/next.mp3", info.Url);
        Assert.Equal("nwtsty", info.PublicationCode);
        Assert.Equal("2", info.Track.TrackCode);
    }

    [Fact]
    public async Task GetInitialTrackInfo_DiscStyleSection_SetsDownloadCodeAndOriginalTrackCode()
    {
        var media = new StubMediaService { TracksToReturn = SingleTrackDict("3") };
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            media,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            biblePublicationService: null);

        var br = new BiblePublicationSchedule
        {
            PublicationCode = "iam",
            LanguageCode = "E",
            SectionCode = "iam-9",
            TrackCode = "3",
        };

        var info = await sut.GetInitialTrackInfo(br);

        Assert.Equal("iam", info.PublicationCode);
        Assert.Equal("iam-9", info.SectionCode);
        Assert.Equal("https://example.test/bible.mp3", info.Url);
    }
}
