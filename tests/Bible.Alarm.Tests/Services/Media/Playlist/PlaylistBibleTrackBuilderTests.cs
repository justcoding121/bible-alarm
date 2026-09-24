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

public sealed class PlaylistBibleTrackBuilderTests
{
    private sealed class StubUrlConstruction : IUrlConstructionService
    {
        public string? PathToReturn { get; init; } = "/lookup";

        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) =>
            Task.FromResult<List<string>>([]);

        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<List<string>>([]);

        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult(PathToReturn);

        public void ClearLookUpPathCache()
        {
        }
    }

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public string? UrlToReturn { get; init; } = "https://example.test/track.mp3";

        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult(UrlToReturn);
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

    private sealed class StubBiblePublicationService : IBiblePublicationService
    {
        public BiblePublication? PublicationWithTracks { get; init; }
        public bool IsNoLanguage { get; init; }

        public void Dispose()
        {
        }

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublicationWithTracks);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(IsNoLanguage);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());
    }

    private static SortedDictionary<string, BiblePublicationTrack> SingleTrackDict(string trackCode) =>
        new(TrackCodeComparer.Comparer)
        {
            [trackCode] = new BiblePublicationTrack { TrackCode = trackCode, Title = "Chapter" },
        };

    private static BiblePublicationSchedule SectionedSchedule(
        string trackCode = "1",
        string sectionCode = "40",
        TimeSpan? finishedDuration = null) =>
        new()
        {
            PublicationCode = "nwtsty",
            LanguageCode = "E",
            SectionCode = sectionCode,
            TrackCode = trackCode,
            FinishedDuration = finishedDuration ?? TimeSpan.Zero,
        };

    private static PlaylistBiblePublicationTrackBuilder CreateSut(
        IMediaService? media = null,
        IMediaUrlRefreshService? urlRefresh = null,
        IUrlConstructionService? urlConstruction = null,
        IBiblePublicationService? biblePublicationService = null) =>
        new(
            TestLogging.CreateLogger(),
            media ?? new StubMediaService { TracksToReturn = SingleTrackDict("1") },
            urlRefresh ?? new StubUrlRefresh(),
            urlConstruction ?? new StubUrlConstruction(),
            biblePublicationService);

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new StubUrlRefresh(),
            new StubUrlConstruction());

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetInitialTrackInfo_SectionedPublication_ResolvesTrackAndUrl()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("1") });

        var info = await sut.GetInitialTrackInfo(SectionedSchedule());

        Assert.Equal("nwtsty", info.PublicationCode);
        Assert.Equal("40", info.SectionCode);
        Assert.Equal("1", info.Track.TrackCode);
        Assert.Equal("https://example.test/track.mp3", info.Url);
    }

    [Fact]
    public async Task GetInitialTrackInfo_NonSectionedPublication_ResolvesViaBibleService()
    {
        var bible = new StubBiblePublicationService
        {
            PublicationWithTracks = new BiblePublication
            {
                PublicationCode = "sjjc",
                Tracks =
                [
                    new BiblePublicationTrack { TrackCode = "5", Title = "Song 5" },
                ],
            },
        };
        var sut = CreateSut(
            media: new StubMediaService(),
            biblePublicationService: bible);

        var schedule = new BiblePublicationSchedule
        {
            PublicationCode = "sjjc",
            LanguageCode = "E",
            SectionCode = null,
            TrackCode = "5",
        };

        var info = await sut.GetInitialTrackInfo(schedule);

        Assert.Equal("sjjc", info.PublicationCode);
        Assert.Null(info.SectionCode);
        Assert.Equal("5", info.Track.TrackCode);
        Assert.Equal("https://example.test/track.mp3", info.Url);
    }

    [Fact]
    public async Task GetInitialTrackInfo_NonSectioned_WithoutBibleService_Throws()
    {
        var sut = CreateSut(biblePublicationService: null);
        var schedule = new BiblePublicationSchedule
        {
            PublicationCode = "sjjc",
            LanguageCode = "E",
            SectionCode = null,
            TrackCode = "1",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInitialTrackInfo(schedule));
    }

    [Fact]
    public async Task GetInitialTrackInfo_Sectioned_TrackMissing_Throws()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("999") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInitialTrackInfo(SectionedSchedule("1")));
    }

    [Fact]
    public async Task GetNextTrackInfo_ResolvesNextTrackInSameSection()
    {
        var sut = CreateSut();
        string? capturedTrack = null;

        var info = await sut.GetNextTrackInfo(
            SectionedSchedule(),
            currentPublicationCode: "nwtsty",
            currentSectionCode: "40",
            currentTrackCode: "1",
            (lang, pub, sec, track) =>
            {
                capturedTrack = track;
                return Task.FromResult(new TrackNavigationResult(
                    "nwtsty",
                    new BiblePublicationSection { SectionCode = "40" },
                    new BiblePublicationTrack { TrackCode = "2", Title = "Next" }));
            });

        Assert.Equal("1", capturedTrack);
        Assert.Equal("nwtsty", info.PublicationCode);
        Assert.Equal("40", info.SectionCode);
        Assert.Equal("2", info.Track.TrackCode);
        Assert.Equal("https://example.test/track.mp3", info.Url);
    }

    [Fact]
    public async Task GetNextTrackInfo_SectionWrap_UsesNextSectionFromNavigation()
    {
        var sut = CreateSut();

        var info = await sut.GetNextTrackInfo(
            SectionedSchedule(trackCode: "50", sectionCode: "40"),
            currentPublicationCode: "nwtsty",
            currentSectionCode: "40",
            currentTrackCode: "50",
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                "nwtsty",
                new BiblePublicationSection { SectionCode = "41" },
                new BiblePublicationTrack { TrackCode = "1", Title = "First of next book" })));

        Assert.Equal("nwtsty", info.PublicationCode);
        Assert.Equal("41", info.SectionCode);
        Assert.Equal("1", info.Track.TrackCode);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_FiniteMode_BuildsRequestedCountAndMarksLast()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("1") });
        var navigations = 0;

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 7,
            new AlarmSchedule { NumberOfTracksToPlay = 3 },
            SectionedSchedule(),
            (_, _, currentSection, currentTrack) =>
            {
                navigations++;
                if (currentSection == "40" && currentTrack == "1")
                {
                    return Task.FromResult(new TrackNavigationResult(
                        "nwtsty",
                        new BiblePublicationSection { SectionCode = "40" },
                        new BiblePublicationTrack { TrackCode = "2" }));
                }

                return Task.FromResult(new TrackNavigationResult(
                    "nwtsty",
                    new BiblePublicationSection { SectionCode = "41" },
                    new BiblePublicationTrack { TrackCode = "1" }));
            });

        Assert.Equal(2, navigations);
        Assert.Equal(3, items.Count);
        Assert.Equal("1", items[0].Metadata.TrackCode);
        Assert.Equal("2", items[1].Metadata.TrackCode);
        Assert.Equal("1", items[2].Metadata.TrackCode);
        Assert.Equal("41", items[2].Metadata.SectionCode);
        Assert.False(items[0].Metadata.IsLastTrack);
        Assert.False(items[1].Metadata.IsLastTrack);
        Assert.True(items[2].Metadata.IsLastTrack);
        Assert.All(items, i => Assert.Equal(7, i.Metadata.ScheduleId));
        Assert.All(items, i => Assert.True(i.Metadata.IsBibleContent));
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_IndefiniteMode_ReturnsSingleTrackWithoutLastFlag()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("1") });
        var navigations = 0;

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 3,
            new AlarmSchedule { NumberOfTracksToPlay = 0 },
            SectionedSchedule(),
            (_, _, _, _) =>
            {
                navigations++;
                return Task.FromResult(new TrackNavigationResult(
                    "nwtsty",
                    new BiblePublicationSection { SectionCode = "40" },
                    new BiblePublicationTrack { TrackCode = "2" }));
            });

        Assert.Equal(0, navigations);
        var single = Assert.Single(items);
        Assert.False(single.Metadata.IsLastTrack);
        Assert.Equal("1", single.Metadata.TrackCode);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_NegativeTrackCount_TreatedAsIndefinite()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("1") });

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 1,
            new AlarmSchedule { NumberOfTracksToPlay = -1 },
            SectionedSchedule(),
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                "nwtsty",
                null,
                new BiblePublicationTrack { TrackCode = "2" })));

        var single = Assert.Single(items);
        Assert.False(single.Metadata.IsLastTrack);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_Resume_AppliesFinishedDurationToFirstMatchingTrackOnly()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("1") });
        var schedule = SectionedSchedule(finishedDuration: TimeSpan.FromSeconds(42));

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 11,
            new AlarmSchedule { NumberOfTracksToPlay = 2, AlwaysPlayFromStart = false },
            schedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                "nwtsty",
                new BiblePublicationSection { SectionCode = "40" },
                new BiblePublicationTrack { TrackCode = "2" })));

        Assert.Equal(2, items.Count);
        Assert.Equal(TimeSpan.FromSeconds(42), items[0].Metadata.FinishedDuration);
        Assert.Equal(TimeSpan.Zero, items[1].Metadata.FinishedDuration);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_AlwaysPlayFromStart_DoesNotApplyFinishedDuration()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("1") });
        var schedule = SectionedSchedule(finishedDuration: TimeSpan.FromSeconds(42));

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 11,
            new AlarmSchedule { NumberOfTracksToPlay = 1, AlwaysPlayFromStart = true },
            schedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                "nwtsty",
                null,
                new BiblePublicationTrack { TrackCode = "2" })));

        var single = Assert.Single(items);
        Assert.Equal(TimeSpan.Zero, single.Metadata.FinishedDuration);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_SectionWrap_AdvancesAcrossSections()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("50") });

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 5,
            new AlarmSchedule { NumberOfTracksToPlay = 2 },
            SectionedSchedule(trackCode: "50", sectionCode: "40"),
            (lang, pub, sec, track) =>
            {
                Assert.Equal("E", lang);
                Assert.Equal("nwtsty", pub);
                Assert.Equal("40", sec);
                Assert.Equal("50", track);
                return Task.FromResult(new TrackNavigationResult(
                    "nwtsty",
                    new BiblePublicationSection { SectionCode = "41" },
                    new BiblePublicationTrack { TrackCode = "1", Title = "Wrapped" }));
            });

        Assert.Equal(2, items.Count);
        Assert.Equal("40", items[0].Metadata.SectionCode);
        Assert.Equal("50", items[0].Metadata.TrackCode);
        Assert.Equal("41", items[1].Metadata.SectionCode);
        Assert.Equal("1", items[1].Metadata.TrackCode);
        Assert.True(items[1].Metadata.IsLastTrack);
    }

    [Fact]
    public async Task BuildBiblePublicationTracks_DiscStyleSection_SetsDownloadAndOriginalTrackCodes()
    {
        var sut = CreateSut(new StubMediaService { TracksToReturn = SingleTrackDict("3") });
        var schedule = new BiblePublicationSchedule
        {
            PublicationCode = "iam",
            LanguageCode = "E",
            SectionCode = "iam-9",
            TrackCode = "3",
        };

        var items = await sut.BuildBiblePublicationTracks(
            scheduleId: 2,
            new AlarmSchedule { NumberOfTracksToPlay = 1 },
            schedule,
            (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                "iam",
                null,
                new BiblePublicationTrack { TrackCode = "4" })));

        var single = Assert.Single(items);
        Assert.Equal("iam-9", single.Metadata.DownloadCode);
        Assert.Equal(3, single.Metadata.OriginalTrackCode);
        Assert.True(single.Metadata.IsLastTrack);
    }

    [Fact]
    public async Task GetNextTrackInfo_EmptyLookupPath_Throws()
    {
        var sut = CreateSut(urlConstruction: new StubUrlConstruction { PathToReturn = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetNextTrackInfo(
                SectionedSchedule(),
                "nwtsty",
                "40",
                "1",
                (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                    "nwtsty",
                    new BiblePublicationSection { SectionCode = "40" },
                    new BiblePublicationTrack { TrackCode = "2" }))));
    }

    [Fact]
    public async Task GetNextTrackInfo_EmptyUrl_Throws()
    {
        var sut = CreateSut(urlRefresh: new StubUrlRefresh { UrlToReturn = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetNextTrackInfo(
                SectionedSchedule(),
                "nwtsty",
                "40",
                "1",
                (_, _, _, _) => Task.FromResult(new TrackNavigationResult(
                    "nwtsty",
                    new BiblePublicationSection { SectionCode = "40" },
                    new BiblePublicationTrack { TrackCode = "2" }))));
    }
}
