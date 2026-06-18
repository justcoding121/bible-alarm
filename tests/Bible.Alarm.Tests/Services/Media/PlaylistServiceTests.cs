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
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlarmSchedule());

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

    private static SortedDictionary<string, BiblePublicationTrack> SingleTrackDict(string trackCode) =>
        new(TrackCodeComparer.Comparer)
        {
            [trackCode] = new BiblePublicationTrack { TrackCode = trackCode, Title = "Chapter" },
        };

    private static PlaylistService CreateSut(
        FakeAlarmScheduleService alarm,
        FakeGeneralSettingsService settings,
        IMediaService? media = null) =>
        new(new PlaylistServiceDeps(
            TestLogging.CreateLogger(),
            media ?? new IdleCatalogMediaService(),
            new NopDispatcher(),
            new FakeApplicationState(new ApplicationState()),
            alarm,
            settings,
            new StubBiblePublicationService(),
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
}
