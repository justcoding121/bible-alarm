#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class DefaultScheduleServiceTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubPlaylistService : IPlaylistService
    {
        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(CreatePlayItem(scheduleId));

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(CreatePlayItem(scheduleId));

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<List<PlayItem>> NextTracks(int scheduleId) => Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult("nwt", null, new BiblePublicationTrack { TrackCode = "1" }));

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult("nwt", null, new BiblePublicationTrack { TrackCode = "1" }));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(CreatePlayItem((int)currentTrackMetadata.ScheduleId));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(CreatePlayItem((int)currentTrackMetadata.ScheduleId));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) => Task.CompletedTask;

        private static PlayItem CreatePlayItem(int scheduleId) =>
            new(
                new TrackMetadata
                {
                    ScheduleId = scheduleId,
                    IsBibleContent = true,
                    LanguageCode = "E",
                    PublicationCode = "nwt",
                    SectionCode = "1",
                    TrackCode = "1",
                    LookUpPath = "/lk",
                },
                "https://cdn.example/track.mp3");
    }

    private sealed class StubPreparePlaybackService : IPreparePlaybackService
    {
        public bool ReturnNullTrack { get; set; }

        public Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<List<AudioPlayerTrack>?>([]);

        public Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ReturnNullTrack
                ? null
                : new AudioPlayerTrack
                {
                    PlayItem = playItem,
                    Uri = playItem.Url,
                });
    }

    private sealed class LastPlayedDbAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(new AlarmSchedule { Id = 10, Name = "First" });

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlarmSchedule());

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(scheduleId == 99);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class StubDisplayMetadataService : IDisplayMetadataService
    {
        public Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track) =>
            Task.FromResult(new MetaData
            {
                Title = "Prepared title",
                Artist = "Prepared artist",
                Album = "Album",
            });

        public Task<MetaData> GetCoreDisplayMetadataAsync(AudioPlayerTrack track) =>
            GetDisplayMetadataAsync(track);
    }

    private sealed class OfflineConnectivityChecker : IInternetConnectivityChecker
    {
        public Task<bool> IsInternetAvailableAsync() => Task.FromResult(false);
    }

    private sealed class IdleAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
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

    private static ScheduleStateItem Schedule(int id, string name, bool isMusic = false) =>
        new()
        {
            Id = id,
            Name = name,
            BiblePublicationIsMusic = isMusic,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "1",
            BiblePublicationTrackCode = "1",
            Hour = 7,
            Minute = 0,
        };

    private static ObservableHashSet<ScheduleStateItem> Schedules(params ScheduleStateItem[] items)
    {
        var set = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in items)
        {
            set.Add(item);
        }

        return set;
    }

    private static DefaultScheduleService CreateSut(
        IState<ApplicationState> state,
        IInternetConnectivityChecker? connectivity = null,
        IAlarmScheduleService? alarmScheduleService = null,
        IPreparePlaybackService? preparePlaybackService = null) =>
        new(
            TestLogging.CreateLogger(),
            state,
            alarmScheduleService ?? new IdleAlarmScheduleService(),
            new StubPlaylistService(),
            preparePlaybackService ?? new StubPreparePlaybackService(),
            new StubDisplayMetadataService(),
            connectivity);

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        DefaultScheduleService sut = null!;
        try
        {
            sut = CreateSut(new FakeApplicationState(new ApplicationState()));
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void ValidateScheduleIdExists_returns_true_when_schedule_in_state()
    {
        using var sut = CreateSut(new FakeApplicationState(new ApplicationState(Schedules(Schedule(5, "Five")))));

        Assert.True(sut.ValidateScheduleIdExists(5));
        Assert.False(sut.ValidateScheduleIdExists(99));
    }

    [Fact]
    public void GetFirstScheduleId_returns_first_or_null()
    {
        using var empty = CreateSut(new FakeApplicationState(new ApplicationState()));
        Assert.Null(empty.GetFirstScheduleId());

        using var withSchedules = CreateSut(new FakeApplicationState(new ApplicationState(Schedules(
            Schedule(12, "Noon")))));
        Assert.Equal(12, withSchedules.GetFirstScheduleId());
    }

    [Fact]
    public async Task GetNextScheduleTrackMetaDataAsync_returns_empty_fallback_when_no_schedules()
    {
        using var sut = CreateSut(new FakeApplicationState(new ApplicationState()));

        var metadata = await sut.GetNextScheduleTrackMetaDataAsync();

        Assert.Equal(0, metadata.ScheduleId);
        Assert.Equal(string.Empty, metadata.Title);
        Assert.Equal(string.Empty, metadata.Artist);
    }

    [Fact]
    public async Task GetNextScheduleTrackMetaDataAsync_prepares_first_schedule_when_state_has_schedules()
    {
        var stateItem = Schedule(42, "Morning Bible");
        using var sut = CreateSut(new FakeApplicationState(new ApplicationState(Schedules(stateItem))));

        var metadata = await sut.GetNextScheduleTrackMetaDataAsync();

        Assert.Equal(42, metadata.ScheduleId);
        Assert.Contains("Morning Bible", metadata.Title, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Artist));
    }

    [Fact]
    public async Task GetNextScheduleTrackMetaDataAsync_offline_uses_listing_metadata_without_prepare()
    {
        var stateItem = Schedule(7, "Offline");
        using var sut = CreateSut(
            new FakeApplicationState(new ApplicationState(Schedules(stateItem))),
            new OfflineConnectivityChecker());

        var metadata = await sut.GetNextScheduleTrackMetaDataAsync();

        Assert.Equal(7, metadata.ScheduleId);
        Assert.Contains("Offline", metadata.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetNextScheduleInRotationMetadataAsync_returns_empty_when_only_music_schedules()
    {
        using var sut = CreateSut(new FakeApplicationState(new ApplicationState(Schedules(
            Schedule(1, "Music only", isMusic: true)))));

        var metadata = await sut.GetNextScheduleInRotationMetadataAsync();

        Assert.Equal(0, metadata.ScheduleId);
        Assert.Equal(string.Empty, metadata.Title);
    }

    [Fact]
    public async Task GetNextScheduleInRotationMetadataAsync_uses_lowest_non_music_schedule_id()
    {
        using var sut = CreateSut(new FakeApplicationState(new ApplicationState(Schedules(
            Schedule(20, "Second"),
            Schedule(10, "First"),
            Schedule(30, "Music", isMusic: true)))));

        var metadata = await sut.GetNextScheduleInRotationMetadataAsync();

        Assert.Equal(10, metadata.ScheduleId);
        Assert.Contains("First", metadata.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetNextScheduleTrackMetaDataAsync_prepare_failure_uses_listing_fallback_metadata()
    {
        var stateItem = Schedule(15, "Prepare fail");
        using var sut = CreateSut(
            new FakeApplicationState(new ApplicationState(Schedules(stateItem))),
            preparePlaybackService: new StubPreparePlaybackService { ReturnNullTrack = true });

        var metadata = await sut.GetNextScheduleTrackMetaDataAsync();

        Assert.Equal(15, metadata.ScheduleId);
        Assert.Contains("Prepare fail", metadata.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetNextScheduleTrackMetaDataAsync_prefers_last_played_schedule_in_state()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        try
        {
            LastPlayedMetadataHelper.ClearLastPlayedMetadata();
            LastPlayedMetadataHelper.SaveLastPlayedMetadata("Last", "Artist", scheduleId: 99);

            var state = new FakeApplicationState(new ApplicationState(Schedules(
                Schedule(10, "First"),
                Schedule(99, "Last played"))));
            using var sut = CreateSut(state);

            var metadata = await sut.GetNextScheduleTrackMetaDataAsync();

            Assert.Equal(99, metadata.ScheduleId);
            Assert.Contains("Last played", metadata.Title, StringComparison.Ordinal);
        }
        finally
        {
            TryClearLastPlayedMetadata();
        }
    }

    [Fact]
    public async Task GetNextScheduleTrackMetaDataAsync_uses_last_played_from_db_when_missing_from_state()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        try
        {
            LastPlayedMetadataHelper.ClearLastPlayedMetadata();
            LastPlayedMetadataHelper.SaveLastPlayedMetadata("Last", "Artist", scheduleId: 99);

            using var sut = CreateSut(
                new FakeApplicationState(new ApplicationState(Schedules(Schedule(10, "In state only")))),
                alarmScheduleService: new LastPlayedDbAlarmScheduleService());

            var metadata = await sut.GetNextScheduleTrackMetaDataAsync();

            Assert.Equal(99, metadata.ScheduleId);
        }
        finally
        {
            TryClearLastPlayedMetadata();
        }
    }

    [Fact]
    public async Task GetNextScheduleInRotationMetadataAsync_advances_to_next_non_music_schedule()
    {
        if (!TryBootstrapMauiAppForPreferences())
        {
            return;
        }

        try
        {
            AndroidAutoRotationHelper.SetLastRotationScheduleId(10);
            using var sut = CreateSut(new FakeApplicationState(new ApplicationState(Schedules(
                Schedule(10, "First"),
                Schedule(20, "Second")))));

            var metadata = await sut.GetNextScheduleInRotationMetadataAsync();

            Assert.Equal(20, metadata.ScheduleId);
            Assert.Contains("Second", metadata.Title, StringComparison.Ordinal);
        }
        finally
        {
            AndroidAutoRotationHelper.SetLastRotationScheduleId(null);
        }
    }

    private static bool TryBootstrapMauiAppForPreferences()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return true;
        }

        try
        {
            MauiAppHolder.CreateAndStore();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryClearLastPlayedMetadata()
    {
        try
        {
            LastPlayedMetadataHelper.ClearLastPlayedMetadata();
        }
        catch
        {
        }
    }
}
