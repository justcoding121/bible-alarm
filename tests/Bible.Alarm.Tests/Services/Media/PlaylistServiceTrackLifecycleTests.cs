#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
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

public sealed class PlaylistServiceTrackLifecycleTests
{
#pragma warning disable CS0067
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Actions { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) => Actions.Add(action);
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
        public void Dispose()
        {
        }

        public Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<GeneralSettings?>(null);

        public Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeAlarmScheduleService : IAlarmScheduleService
    {
        public Dictionary<int, AlarmSchedule> ById { get; } = [];

        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.GetValueOrDefault(scheduleId));

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

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
            Task.FromResult(ById.ContainsKey(scheduleId));

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.Count > 0);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.GetValueOrDefault(scheduleId)?.Music);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.GetValueOrDefault(scheduleId)?.BiblePublicationSchedule);
    }

    private sealed class StubBiblePublicationService : IBiblePublicationService
    {
        public BiblePublication? Publication { get; init; }

        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Publication is not null
                && string.Equals(Publication.PublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                ? Publication
                : null);

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

        public SortedDictionary<int, MusicTrack>? VocalTracks { get; init; }

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

    private static BiblePublication FlatThreeTrackPublication(string code = "vod") =>
        new()
        {
            PublicationCode = code,
            IsVideo = false,
            IsMusic = false,
            Tracks =
            [
                new BiblePublicationTrack { TrackCode = "1", Title = "First" },
                new BiblePublicationTrack { TrackCode = "2", Title = "Second" },
                new BiblePublicationTrack { TrackCode = "3", Title = "Third" },
            ],
        };

    private static SortedDictionary<int, MusicTrack> TwoVocalTracks() =>
        new()
        {
            [1] = new MusicTrack { TrackCode = "1", Title = "Song A" },
            [2] = new MusicTrack { TrackCode = "2", Title = "Song B" },
        };

    private static AlarmSchedule FlatBibleSchedule(int id, string trackCode = "1", string publicationCode = "vod") =>
        new()
        {
            Id = id,
            MusicEnabled = false,
            NumberOfTracksToPlay = 1,
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = publicationCode,
                LanguageCode = "E",
                SectionCode = null,
                TrackCode = trackCode,
                FinishedDuration = TimeSpan.Zero,
            },
        };

    private static TrackMetadata BibleMetadata(int scheduleId, string trackCode, string publicationCode = "vod", TimeSpan? finished = null) =>
        new()
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = publicationCode,
            SectionCode = null,
            TrackCode = trackCode,
            FinishedDuration = finished ?? TimeSpan.Zero,
        };

    private static TrackMetadata MusicMetadata(int scheduleId, string trackCode, string publicationCode = "sjjc") =>
        new()
        {
            ScheduleId = scheduleId,
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = publicationCode,
            TrackCode = trackCode,
        };

    private static PlaylistService CreateSut(
        FakeAlarmScheduleService alarm,
        RecordingDispatcher? dispatcher = null,
        IMediaService? media = null,
        IBiblePublicationService? bible = null) =>
        new(new PlaylistServiceDeps(
            TestLogging.CreateLogger(),
            media ?? new IdleCatalogMediaService(),
            dispatcher ?? new RecordingDispatcher(),
            new FakeApplicationState(new ApplicationState()),
            alarm,
            new FakeGeneralSettingsService(),
            bible ?? new StubBiblePublicationService { Publication = FlatThreeTrackPublication() },
            new StubUrlRefresh(),
            new StubUrlConstruction()));

    [Fact]
    public async Task MarkTrackAsPlayed_persists_bible_finished_duration()
    {
        const int scheduleId = 21;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId);
        using var sut = CreateSut(alarm);
        var metadata = BibleMetadata(scheduleId, "1", finished: TimeSpan.FromSeconds(42));

        await sut.MarkTrackAsPlayed(metadata);

        Assert.Equal(TimeSpan.FromSeconds(42), alarm.ById[scheduleId].BiblePublicationSchedule!.FinishedDuration);
        Assert.Equal("1", alarm.ById[scheduleId].BiblePublicationSchedule!.TrackCode);
    }

    [Fact]
    public async Task MarkTrackAsPlayed_dispatches_when_bible_track_changed()
    {
        const int scheduleId = 22;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "1");
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(alarm, dispatcher);
        var metadata = BibleMetadata(scheduleId, "2", finished: TimeSpan.FromSeconds(5));

        await sut.MarkTrackAsPlayed(metadata);

        var action = Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Actions));
        Assert.Equal("2", action.Schedule.BiblePublicationSchedule!.TrackCode);
    }

    [Fact]
    public async Task MarkTrackAsPlayed_does_not_dispatch_when_same_bible_track()
    {
        const int scheduleId = 23;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "1");
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(alarm, dispatcher);

        await sut.MarkTrackAsPlayed(BibleMetadata(scheduleId, "1", finished: TimeSpan.FromSeconds(10)));

        Assert.Empty(dispatcher.Actions);
    }

    [Fact]
    public async Task MarkTrackAsFinished_advances_bible_pointer_and_dispatches()
    {
        const int scheduleId = 31;
        var pub = FlatThreeTrackPublication();
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "1", publicationCode: pub.PublicationCode);
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(alarm, dispatcher, bible: new StubBiblePublicationService { Publication = pub });

        await sut.MarkTrackAsFinished(BibleMetadata(scheduleId, "1", pub.PublicationCode));

        var bible = alarm.ById[scheduleId].BiblePublicationSchedule!;
        Assert.Equal("2", bible.TrackCode);
        Assert.Equal(TimeSpan.Zero, bible.FinishedDuration);
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Actions));
    }

    [Fact]
    public async Task NextTrack_throws_for_invalid_schedule_id()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.NextTrack(404));
    }

    [Fact]
    public async Task NextTrack_returns_bible_play_item_when_music_disabled()
    {
        const int scheduleId = 41;
        var pub = FlatThreeTrackPublication();
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "2", publicationCode: pub.PublicationCode);
        using var sut = CreateSut(alarm, bible: new StubBiblePublicationService { Publication = pub });

        var item = await sut.NextTrack(scheduleId);

        Assert.Equal("https://example.test/bible.mp3", item.Url);
        Assert.True(item.Metadata.IsBibleContent);
        Assert.Equal("2", item.Metadata.TrackCode);
        Assert.Equal(scheduleId, item.Metadata.ScheduleId);
    }

    [Fact]
    public async Task NextTrack_returns_music_play_item_when_music_enabled()
    {
        const int scheduleId = 42;
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
        };
        var media = new StubMediaService { VocalTracks = TwoVocalTracks() };
        using var sut = CreateSut(alarm, media: media);

        var item = await sut.NextTrack(scheduleId);

        Assert.Equal("1", item.Metadata.TrackCode);
        Assert.False(item.Metadata.IsBibleContent);
        Assert.Equal("https://example.test/bible.mp3", item.Url);
    }

    [Fact]
    public async Task NextBiblePublicationTrack_returns_null_when_bible_missing()
    {
        const int scheduleId = 51;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule { Id = scheduleId, BiblePublicationSchedule = null };
        using var sut = CreateSut(alarm);

        Assert.Null(await sut.NextBiblePublicationTrack(scheduleId));
    }

    [Fact]
    public async Task NextBiblePublicationTrack_returns_play_item_when_configured()
    {
        const int scheduleId = 52;
        var pub = FlatThreeTrackPublication();
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "1", publicationCode: pub.PublicationCode);
        using var sut = CreateSut(alarm, bible: new StubBiblePublicationService { Publication = pub });

        var item = await sut.NextBiblePublicationTrack(scheduleId);

        Assert.NotNull(item);
        Assert.Equal("1", item!.Metadata.TrackCode);
        Assert.Equal(pub.PublicationCode, item.Metadata.PublicationCode);
    }

    [Fact]
    public async Task GetNextPlayItemAsync_throws_when_metadata_null()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService());

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetNextPlayItemAsync(null!));
    }

    [Fact]
    public async Task GetNextPlayItemAsync_returns_next_bible_track()
    {
        var pub = FlatThreeTrackPublication();
        using var sut = CreateSut(
            new FakeAlarmScheduleService(),
            bible: new StubBiblePublicationService { Publication = pub });

        var item = await sut.GetNextPlayItemAsync(BibleMetadata(10, "1", pub.PublicationCode));

        Assert.Equal("2", item.Metadata.TrackCode);
        Assert.Equal(pub.PublicationCode, item.Metadata.PublicationCode);
        Assert.Equal("https://example.test/bible.mp3", item.Url);
    }

    [Fact]
    public async Task GetNextPlayItemAsync_returns_next_music_track()
    {
        const int scheduleId = 61;
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
        var media = new StubMediaService { VocalTracks = TwoVocalTracks() };
        using var sut = CreateSut(alarm, media: media);

        var item = await sut.GetNextPlayItemAsync(MusicMetadata(scheduleId, "1"));

        Assert.Equal("2", item.Metadata.TrackCode);
        Assert.False(item.Metadata.IsBibleContent);
    }

    [Fact]
    public async Task GetPreviousPlayItemAsync_throws_when_metadata_null()
    {
        using var sut = CreateSut(new FakeAlarmScheduleService());

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetPreviousPlayItemAsync(null!));
    }

    [Fact]
    public async Task GetPreviousPlayItemAsync_returns_previous_bible_track()
    {
        var pub = FlatThreeTrackPublication();
        using var sut = CreateSut(
            new FakeAlarmScheduleService(),
            bible: new StubBiblePublicationService { Publication = pub });

        var item = await sut.GetPreviousPlayItemAsync(BibleMetadata(10, "2", pub.PublicationCode));

        Assert.Equal("1", item.Metadata.TrackCode);
        Assert.Equal("https://example.test/bible.mp3", item.Url);
    }

    [Fact]
    public async Task GetPreviousPlayItemAsync_returns_previous_music_track()
    {
        const int scheduleId = 62;
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = new AlarmSchedule
        {
            Id = scheduleId,
            Music = new AlarmMusic
            {
                LanguageCode = "E",
                PublicationCode = "sjjc",
                TrackCode = "2",
            },
        };
        var media = new StubMediaService { VocalTracks = TwoVocalTracks() };
        using var sut = CreateSut(alarm, media: media);

        var item = await sut.GetPreviousPlayItemAsync(MusicMetadata(scheduleId, "2"));

        Assert.Equal("1", item.Metadata.TrackCode);
    }

    [Fact]
    public async Task MoveToNextBiblePublicationTrack_updates_schedule_pointer()
    {
        const int scheduleId = 71;
        var pub = FlatThreeTrackPublication();
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "1", publicationCode: pub.PublicationCode);
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(alarm, dispatcher, bible: new StubBiblePublicationService { Publication = pub });

        await sut.MoveToNextBiblePublicationTrack(scheduleId);

        Assert.Equal("2", alarm.ById[scheduleId].BiblePublicationSchedule!.TrackCode);
        Assert.Equal(TimeSpan.Zero, alarm.ById[scheduleId].BiblePublicationSchedule!.FinishedDuration);
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Actions));
    }

    [Fact]
    public async Task MoveToPreviousBiblePublicationTrack_updates_schedule_pointer()
    {
        const int scheduleId = 72;
        var pub = FlatThreeTrackPublication();
        var alarm = new FakeAlarmScheduleService();
        alarm.ById[scheduleId] = FlatBibleSchedule(scheduleId, trackCode: "2", publicationCode: pub.PublicationCode);
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(alarm, dispatcher, bible: new StubBiblePublicationService { Publication = pub });

        await sut.MoveToPreviousBiblePublicationTrack(scheduleId);

        Assert.Equal("1", alarm.ById[scheduleId].BiblePublicationSchedule!.TrackCode);
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Actions));
    }
}
