#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaylistServiceTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public string UrlToReturn { get; init; } = "https://example.test/bible.mp3";

        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult<string?>(UrlToReturn);
    }

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

    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class FakeGeneralSettingsService : IGeneralSettingsService
    {
        public GeneralSettings? LastPlayedRow { get; set; }

        public List<(string Key, string Value)> Sets { get; } = [];

        public void Dispose()
        {
        }

        public Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default)
        {
            if (key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId)
            {
                return Task.FromResult(LastPlayedRow);
            }

            return Task.FromResult<GeneralSettings?>(null);
        }

        public Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Sets.Add((key, value));
            return Task.CompletedTask;
        }

        public Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId && LastPlayedRow != null);
    }

    private sealed class FakeAlarmScheduleService : IAlarmScheduleService
    {
        public Dictionary<int, AlarmSchedule> ById { get; } = [];

        public AlarmSchedule? First { get; set; }

        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.GetValueOrDefault(scheduleId));

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(First);

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            var schedule = ById.GetValueOrDefault(scheduleId)
                ?? throw new InvalidOperationException($"Schedule {scheduleId} not found");
            updateAction(schedule);
            return Task.FromResult(schedule);
        }

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class StubBiblePublicationService : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private class StubMediaService : IMediaService
    {
        public SortedDictionary<string, BiblePublicationTrack>? TracksToReturn { get; init; }

        public SortedDictionary<int, MusicTrack>? VocalTracks { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public virtual Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(TracksToReturn ?? new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer));

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public virtual Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
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
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private static SortedDictionary<string, BiblePublicationTrack> SingleTrackDict(string trackCode) =>
        new(TrackCodeComparer.Comparer)
        {
            [trackCode] = new BiblePublicationTrack { TrackCode = trackCode, Title = "Chapter" },
        };

    private static PlaylistService CreateSut(
        FakeAlarmScheduleService alarm,
        FakeGeneralSettingsService settings,
        IMediaService? media = null,
        IBiblePublicationService? bible = null) =>
        new(new PlaylistServiceDeps(
            TestLogging.CreateLogger(),
            media ?? new IdleCatalogMediaService(),
            new NopDispatcher(),
            new FakeApplicationState(new ApplicationState()),
            alarm,
            settings,
            bible ?? new StubBiblePublicationService(),
            new StubUrlRefresh(),
            new StubUrlConstruction()));

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var deps = new PlaylistServiceDeps(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new NopDispatcher(),
            null!,
            null!,
            null!,
            null!,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            LanguageContentService: null,
            ScopeFactory: null,
            ScheduleDisplayNameService: null);

        using var sut = new PlaylistService(deps);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetRelevantScheduleToPlay_returns_last_played_schedule_id()
    {
        var settings = new FakeGeneralSettingsService
        {
            LastPlayedRow = new GeneralSettings { Value = "7" },
        };
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[7] = new AlarmSchedule { Id = 7 };
        alarm.First = new AlarmSchedule { Id = 1 };
        using var sut = CreateSut(alarm, settings);

        Assert.Equal(7, await sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task GetRelevantScheduleToPlay_falls_back_to_first_when_last_missing()
    {
        var settings = new FakeGeneralSettingsService { LastPlayedRow = null };
        var alarm = new FakeAlarmScheduleService { First = new AlarmSchedule { Id = 3 } };
        using var sut = CreateSut(alarm, settings);

        Assert.Equal(3, await sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task SaveLastPlayed_persists_last_played_setting()
    {
        var settings = new FakeGeneralSettingsService();
        var alarm = new FakeAlarmScheduleService();
        using var sut = CreateSut(alarm, settings);

        await sut.SaveLastPlayed(11);

        var call = Assert.Single(settings.Sets);
        Assert.Equal(AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, call.Key);
    }

    [Fact]
    public async Task ShouldResumeFromLastPositionAsync_false_when_always_play_from_start()
    {
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[5] = new AlarmSchedule { Id = 5, AlwaysPlayFromStart = true };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService());

        Assert.False(await sut.ShouldResumeFromLastPositionAsync(5));
    }

    [Fact]
    public async Task ShouldResumeFromLastPositionAsync_true_when_resume_allowed()
    {
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[6] = new AlarmSchedule { Id = 6, AlwaysPlayFromStart = false };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService());

        Assert.True(await sut.ShouldResumeFromLastPositionAsync(6));
    }

    [Fact]
    public async Task GetScheduleFinishedDurationAsync_returns_bible_finished_duration()
    {
        var expected = TimeSpan.FromSeconds(90);
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[8] = new AlarmSchedule
        {
            Id = 8,
            BiblePublicationSchedule = new BiblePublicationSchedule { FinishedDuration = expected },
        };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService());

        Assert.Equal(expected, await sut.GetScheduleFinishedDurationAsync(8));
    }

    [Fact]
    public async Task NextTracks_throws_for_invalid_schedule_id()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.NextTracks(0));
    }

    [Fact]
    public async Task NextTracks_builds_bible_playlist_when_bible_configured()
    {
        const int scheduleId = 9;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            NumberOfTracksToPlay = 1,
            MusicEnabled = false,
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwtsty",
                LanguageCode = "E",
                SectionCode = "40",
                TrackCode = "2",
            },
        };

        var media = new StubMediaService { TracksToReturn = SingleTrackDict("2") };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService(), media);

        var items = await sut.NextTracks(scheduleId);

        Assert.Equal("https://example.test/bible.mp3", Assert.Single(items).Url);
    }

    [Fact]
    public async Task PersistSchedulePointerToFinishedTrackAsync_no_ops_for_invalid_schedule_id()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());
        var metadata = new TrackMetadata { ScheduleId = 0, TrackCode = "1" };

        var ex = await Record.ExceptionAsync(() => sut.PersistSchedulePointerToFinishedTrackAsync(metadata));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ShouldResumeFromLastPositionAsync_false_when_schedule_missing()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());

        Assert.False(await sut.ShouldResumeFromLastPositionAsync(404));
    }

    [Fact]
    public async Task GetRelevantScheduleToPlay_falls_back_when_last_played_schedule_not_found()
    {
        var settings = new FakeGeneralSettingsService
        {
            LastPlayedRow = new GeneralSettings { Value = "999" },
        };
        var alarm = new FakeAlarmScheduleService { First = new AlarmSchedule { Id = 4 } };
        using var sut = CreateSut(alarm, settings);

        Assert.Equal(4, await sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task NextTracks_includes_music_and_bible_when_both_configured()
    {
        const int scheduleId = 15;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            NumberOfTracksToPlay = 1,
            MusicEnabled = true,
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "sjjc",
                TrackCode = "1",
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwtsty",
                LanguageCode = "E",
                SectionCode = "40",
                TrackCode = "2",
            },
        };

        var media = new StubMediaService
        {
            TracksToReturn = SingleTrackDict("2"),
            VocalTracks = new SortedDictionary<int, MusicTrack>
            {
                [1] = new MusicTrack { TrackCode = "1", Title = "Song" },
            },
        };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService(), media);

        var items = await sut.NextTracks(scheduleId);

        Assert.Equal(2, items.Count);
        Assert.False(items[0].Metadata.IsBibleContent);
        Assert.True(items[1].Metadata.IsBibleContent);
    }

    [Fact]
    public async Task MoveToNextBiblePublicationTrack_throws_when_bible_schedule_missing()
    {
        const int scheduleId = 16;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule { Id = scheduleId, BiblePublicationSchedule = null };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSutWithDispatcher(alarm, dispatcher);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.MoveToNextBiblePublicationTrack(scheduleId));
        Assert.Empty(dispatcher.Actions);
    }

    [Fact]
    public async Task PersistSchedulePointerToFinishedTrackAsync_updates_bible_track_and_dispatches()
    {
        const int scheduleId = 17;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                LanguageCode = "E",
                TrackCode = "1",
            },
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSutWithDispatcher(alarm, dispatcher);
        var metadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            TrackCode = "3",
        };

        await sut.PersistSchedulePointerToFinishedTrackAsync(metadata);

        Assert.Equal("3", alarm.ById[scheduleId].BiblePublicationSchedule!.TrackCode);
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Actions));
    }

    [Fact]
    public void Dispose_can_be_called_twice_without_throw()
    {
        var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public async Task GetRelevantScheduleToPlay_throws_when_no_schedules_exist()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task NextTracks_returns_music_only_when_bible_not_configured()
    {
        const int scheduleId = 18;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            MusicEnabled = true,
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "sjjc",
                TrackCode = "1",
            },
            BiblePublicationSchedule = null,
        };
        var media = new StubMediaService
        {
            VocalTracks = new SortedDictionary<int, MusicTrack>
            {
                [1] = new MusicTrack { TrackCode = "1", Title = "Song" },
            },
        };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService(), media);

        var items = await sut.NextTracks(scheduleId);

        var item = Assert.Single(items);
        Assert.False(item.Metadata.IsBibleContent);
    }

    [Fact]
    public async Task GetNextBiblePublicationTrack_delegates_to_track_navigator()
    {
        var pub = new BiblePublication
        {
            PublicationCode = "vod",
            Tracks =
            [
                new BiblePublicationTrack { TrackCode = "1", Title = "A" },
                new BiblePublicationTrack { TrackCode = "2", Title = "B" },
            ],
        };
        using var sut = CreateSut(
            new FakeAlarmScheduleService(),
            new FakeGeneralSettingsService(),
            bible: new TrackingBiblePublicationService(pub));

        var next = await sut.GetNextBiblePublicationTrack("E", "vod", null, "1");

        Assert.Equal("2", next.Track.TrackCode);
    }

    [Fact]
    public async Task MoveToPreviousBiblePublicationTrack_throws_when_bible_schedule_missing()
    {
        const int scheduleId = 19;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule { Id = scheduleId, BiblePublicationSchedule = null };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSutWithDispatcher(alarm, dispatcher);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.MoveToPreviousBiblePublicationTrack(scheduleId));
        Assert.Empty(dispatcher.Actions);
    }

    [Fact]
    public async Task GetScheduleFinishedDurationAsync_returns_zero_when_schedule_missing()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());

        Assert.Equal(TimeSpan.Zero, await sut.GetScheduleFinishedDurationAsync(12345));
    }

    [Fact]
    public async Task GetPreviousBiblePublicationTrack_delegates_to_track_navigator()
    {
        var pub = new BiblePublication
        {
            PublicationCode = "vod",
            Tracks =
            [
                new BiblePublicationTrack { TrackCode = "1", Title = "A" },
                new BiblePublicationTrack { TrackCode = "2", Title = "B" },
            ],
        };
        using var sut = CreateSut(
            new FakeAlarmScheduleService(),
            new FakeGeneralSettingsService(),
            bible: new TrackingBiblePublicationService(pub));

        var prev = await sut.GetPreviousBiblePublicationTrack("E", "vod", null, "2");

        Assert.Equal("1", prev.Track.TrackCode);
    }

    [Fact]
    public async Task GetNextBiblePublicationSection_delegates_to_track_navigator()
    {
        var owningPub = new BiblePublication { PublicationCode = "nwt", Id = 1 };
        var sections = new SortedDictionary<string, BiblePublicationSection>(Comparer<string>.Create((a, b) =>
            SectionCodeHelper.SectionCodeComparer.Compare(a, b)))
        {
            ["10"] = new BiblePublicationSection { SectionCode = "10", Name = "Ten", BiblePublication = owningPub },
            ["20"] = new BiblePublicationSection { SectionCode = "20", Name = "Twenty", BiblePublication = owningPub },
        };
        var media = new SectionedMediaStub
        {
            Sections = sections,
            TracksBySection =
            {
                ["10"] = SingleTrackDict("1"),
                ["20"] = SingleTrackDict("1"),
            },
        };
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService(), media);

        var next = await sut.GetNextBiblePublicationSection("E", "nwt", "10");

        Assert.Equal("20", next.Key);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationSection_delegates_to_track_navigator()
    {
        var owningPub = new BiblePublication { PublicationCode = "nwt", Id = 1 };
        var sections = new SortedDictionary<string, BiblePublicationSection>(Comparer<string>.Create((a, b) =>
            SectionCodeHelper.SectionCodeComparer.Compare(a, b)))
        {
            ["10"] = new BiblePublicationSection { SectionCode = "10", Name = "Ten", BiblePublication = owningPub },
            ["20"] = new BiblePublicationSection { SectionCode = "20", Name = "Twenty", BiblePublication = owningPub },
        };
        var media = new SectionedMediaStub
        {
            Sections = sections,
            TracksBySection =
            {
                ["10"] = SingleTrackDict("1"),
                ["20"] = SingleTrackDict("1"),
            },
        };
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService(), media);

        var prev = await sut.GetPreviousBiblePublicationSection("E", "nwt", "20");

        Assert.Equal("10", prev.Key);
    }

    [Fact]
    public async Task PersistSchedulePointerToFinishedTrackAsync_updates_music_track()
    {
        const int scheduleId = 20;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "sjjc",
                TrackCode = "1",
            },
        };
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSutWithDispatcher(alarm, dispatcher);
        var metadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "sjjc",
            TrackCode = "3",
            DownloadCode = "disc-1",
        };

        await sut.PersistSchedulePointerToFinishedTrackAsync(metadata);

        Assert.Equal("3", alarm.ById[scheduleId].Music!.TrackCode);
        Assert.Equal("disc-1", alarm.ById[scheduleId].Music!.SectionCode);
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Actions));
    }

    [Fact]
    public async Task NextTrack_throws_when_neither_music_nor_bible_configured()
    {
        const int scheduleId = 21;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            MusicEnabled = false,
            Music = null,
            BiblePublicationSchedule = null,
        };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.NextTrack(scheduleId));
    }

    [Fact]
    public async Task GetNextPlayItemAsync_music_throws_when_schedule_id_invalid()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());
        var metadata = new TrackMetadata
        {
            ScheduleId = 0,
            IsBibleContent = false,
            TrackCode = "1",
            PublicationCode = "sjjc",
            LanguageCode = "E",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetNextPlayItemAsync(metadata));
    }

    [Fact]
    public async Task GetPreviousPlayItemAsync_music_throws_when_music_missing()
    {
        const int scheduleId = 22;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule { Id = scheduleId, Music = null };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService());
        var metadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = false,
            TrackCode = "1",
            PublicationCode = "sjjc",
            LanguageCode = "E",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetPreviousPlayItemAsync(metadata));
    }

    [Fact]
    public async Task GetScheduleFinishedDurationAsync_returns_zero_when_bible_schedule_null()
    {
        const int scheduleId = 23;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule { Id = scheduleId, BiblePublicationSchedule = null };
        using var sut = CreateSut(alarm, new FakeGeneralSettingsService());

        Assert.Equal(TimeSpan.Zero, await sut.GetScheduleFinishedDurationAsync(scheduleId));
    }

    [Fact]
    public async Task NextBiblePublicationTrack_throws_for_missing_schedule()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService(), new FakeGeneralSettingsService());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.NextBiblePublicationTrack(404));
    }

    private sealed class SectionedMediaStub : StubMediaService
    {
        public required SortedDictionary<string, BiblePublicationSection> Sections { get; init; }

        public Dictionary<string, SortedDictionary<string, BiblePublicationTrack>> TracksBySection { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public override Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(
            string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(Sections);

        public override Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(
            string languageCode, string versionCode, string? sectionCode)
        {
            var key = string.IsNullOrWhiteSpace(sectionCode) ? string.Empty : sectionCode;
            if (TracksBySection.TryGetValue(key, out var tracks))
            {
                return Task.FromResult(tracks);
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer));
        }
    }

    private sealed class TrackingBiblePublicationService(BiblePublication publication) : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(
                string.Equals(publication.PublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                    ? publication
                    : null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());
    }

#pragma warning disable CS0067
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Actions { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) => Actions.Add(action);
    }
#pragma warning restore CS0067

    private static PlaylistService CreateSutWithDispatcher(
        FakeAlarmScheduleService alarm,
        RecordingDispatcher dispatcher,
        IMediaService? media = null) =>
        new(new PlaylistServiceDeps(
            TestLogging.CreateLogger(),
            media ?? new IdleCatalogMediaService(),
            dispatcher,
            new FakeApplicationState(new ApplicationState()),
            alarm,
            new FakeGeneralSettingsService(),
            new StubBiblePublicationService(),
            new StubUrlRefresh(),
            new StubUrlConstruction()));
}
