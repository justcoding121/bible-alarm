#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackServiceTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
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

    private sealed class StubPreparePlaybackService : IPreparePlaybackService
    {
        public Func<int, CancellationToken, Task<List<AudioPlayerTrack>?>>? PrepareTracksImpl { get; set; }

        public Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            PrepareTracksImpl != null
                ? PrepareTracksImpl(scheduleId, cancellationToken)
                : Task.FromResult<List<AudioPlayerTrack>?>(null);

        public Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<AudioPlayerTrack?>(null);
    }

    private sealed class MinimalPlaylistService : IPlaylistService
    {
        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) => Task.FromResult(CreatePlayItem(scheduleId));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) => Task.FromResult<PlayItem?>(CreatePlayItem(scheduleId));

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
            throw new InvalidOperationException("not used in this test");

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null) =>
            throw new InvalidOperationException("not used in this test");

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
                "file://track.mp3");
    }

    private sealed class ScheduleReturningAlarmService : IAlarmScheduleService
    {
        private readonly AlarmSchedule schedule;

        public ScheduleReturningAlarmService(int scheduleId) =>
            schedule = new AlarmSchedule { Id = scheduleId, NumberOfTracksToPlay = 1 };

        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(scheduleId == schedule.Id ? schedule : null);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            if (scheduleId == schedule.Id)
            {
                updateAction(schedule);
            }

            return Task.FromResult(schedule);
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

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule, CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

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

    private sealed class NopNotificationService : INotificationService
    {
        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body) =>
            Task.CompletedTask;

        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(false);

        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> CanScheduleAsync() => Task.FromResult(true);
    }

    private sealed class NopRingtoneService : IDefaultDeviceRingtoneService
    {
        public void StartLoopingAlarmRingtone()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class NopFallbackAlarmSoundService : IFallbackAlarmSoundService
    {
        public Task<AudioPlayerTrack?> GetFallbackAlarmTrackAsync() => Task.FromResult<AudioPlayerTrack?>(null);
    }

    private sealed class RecordingFallbackAlarmSoundService : IFallbackAlarmSoundService
    {
        public int CallCount { get; private set; }

        public AudioPlayerTrack? TrackToReturn { get; init; }

        public Task<AudioPlayerTrack?> GetFallbackAlarmTrackAsync()
        {
            CallCount++;
            return Task.FromResult(TrackToReturn);
        }
    }

    private sealed class NopCdnProbe : ICdnPlaybackUrlProbe
    {
        public Task<CdnUrlProbeOutcome> ProbeStreamingUrlAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(CdnUrlProbeOutcome.Indeterminate);
    }

    private sealed class NopTrackCdnRefresher : ITrackCdnUrlRefresher
    {
        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NopMediaCacheService : IMediaCacheService
    {
        public void Dispose()
        {
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => lookUpPath;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => lookUpPath;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) => Task.FromResult(true);

        public Task CleanUpAsync() => Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(playItem.Url);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) => Task.CompletedTask;
    }

    private sealed class RecordingAudioPlayer : IAudioPlayer
    {
        public int PauseCallCount { get; private set; }
        public int PrepareCallCount { get; private set; }
        public int PlayCallCount { get; private set; }
        public int ResumeCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public int SeekCallCount { get; private set; }

        public void Dispose()
        {
        }

        public Task PrepareAsync(AudioPlayerTrack track)
        {
            PrepareCallCount++;
            return Task.CompletedTask;
        }

        public Task PlayAsync()
        {
            PlayCallCount++;
            Status = PlayStatus.Playing;
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            PauseCallCount++;
            Status = PlayStatus.Paused;
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            ResumeCallCount++;
            Status = PlayStatus.Playing;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCallCount++;
            Status = PlayStatus.Stopped;
            return Task.CompletedTask;
        }

        public Task ResetAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position)
        {
            SeekCallCount++;
            return Task.CompletedTask;
        }

        public Task SetMutedAsync(bool muted) => Task.CompletedTask;

        public void NotifyTrackTransitionStarting()
        {
        }

        public void RaiseMediaEnded() => MediaEnded?.Invoke(this, EventArgs.Empty);

        public void RaiseMediaFailed() => MediaFailed?.Invoke(this, EventArgs.Empty);

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public TimeSpan? CurrentPosition => TimeSpan.FromSeconds(10);

        public TimeSpan Duration => TimeSpan.FromMinutes(5);

        public PlayStatus Status { get; set; } = PlayStatus.Stopped;

        public bool IsActuallyPlayingOrPaused => Status is PlayStatus.Playing or PlayStatus.Paused;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;
        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067
    }

    private static PlaybackServiceInjectionContext CreateInjection(
        IPreparePlaybackService? prepare = null,
        IPlaylistService? playlist = null,
        IAlarmScheduleService? alarm = null,
        IFallbackAlarmSoundService? fallback = null) =>
        new(
            prepare ?? new StubPreparePlaybackService(),
            playlist ?? new MinimalPlaylistService(),
            fallback ?? new NopFallbackAlarmSoundService(),
            new NopMediaCacheService(),
            new NopCdnProbe(),
            new NopTrackCdnRefresher(),
            new NopNotificationService(),
            new NopRingtoneService(),
            new SyncMainThreadScheduler());

    private static PlaybackService CreateSut(
        IAudioPlayer audioPlayer,
        IState<PlaybackState> playbackState,
        IDispatcher? dispatcher = null,
        PlaybackServiceInjectionContext? injection = null,
        IAlarmScheduleService? alarmSchedule = null) =>
        new(
            TestLogging.CreateLogger(),
            audioPlayer,
            alarmSchedule ?? new IdleAlarmScheduleService(),
            dispatcher ?? new RecordingDispatcher(),
            playbackState,
            injection ?? CreateInjection());

    private static AudioPlayerTrack CreatePreparedTrack(int scheduleId)
    {
        var item = new PlayItem(
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
            "file://cached/track.mp3");

        return new AudioPlayerTrack { PlayItem = item, Uri = item.Url };
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var injection = new PlaybackServiceInjectionContext(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new SyncMainThreadScheduler());

        PlaybackService sut = null!;
        try
        {
            sut = new PlaybackService(
                TestLogging.CreateLogger(),
                new RecordingAudioPlayer(),
                new IdleAlarmScheduleService(),
                new RecordingDispatcher(),
                new FakePlaybackState(new PlaybackState()),
                injection);

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Receive_PauseButtonPressedMessage_invokes_pause_on_main_thread()
    {
        var player = new RecordingAudioPlayer { Status = PlayStatus.Playing };
        using var sut = CreateSut(player, new FakePlaybackState(new PlaybackState()));

        sut.Receive(new PauseButtonPressedMessage());

        Assert.True(WaitForPause(player, TimeSpan.FromSeconds(5)));
        Assert.Equal(1, player.PauseCallCount);
    }

    [Fact]
    public void Receive_TogglePlayPauseMessage_when_playing_invokes_pause()
    {
        var player = new RecordingAudioPlayer { Status = PlayStatus.Playing };
        using var sut = CreateSut(player, new FakePlaybackState(new PlaybackState { Status = PlayStatus.Playing }));

        sut.Receive(new TogglePlayPauseMessage());

        Assert.True(WaitForPause(player, TimeSpan.FromSeconds(5)));
        Assert.Equal(1, player.PauseCallCount);
    }

    private static bool WaitForPause(RecordingAudioPlayer player, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (player.PauseCallCount > 0)
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    [Fact]
    public void Receive_NextButtonPressedMessage_does_not_throw_with_empty_playlist()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = Record.Exception(() => sut.Receive(new NextButtonPressedMessage()));

        Assert.Null(ex);
    }

    [Fact]
    public void Receive_PreviousButtonPressedMessage_does_not_throw_with_empty_playlist()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = Record.Exception(() => sut.Receive(new PreviousButtonPressedMessage()));

        Assert.Null(ex);
    }

    [Fact]
    public void Receive_SeekBackwardButtonPressedMessage_does_not_throw()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = Record.Exception(() => sut.Receive(new SeekBackwardButtonPressedMessage()));

        Assert.Null(ex);
    }

    [Fact]
    public void Receive_SeekForwardButtonPressedMessage_does_not_throw()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = Record.Exception(() => sut.Receive(new SeekForwardButtonPressedMessage()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_when_prepare_returns_null_does_not_throw()
    {
        const int scheduleId = 7;
        var dispatcher = new RecordingDispatcher();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>(null),
        };
        var injection = CreateInjection(prepare);
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            dispatcher,
            injection,
            new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackStartedAction);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_when_prepare_returns_empty_list_does_not_throw()
    {
        const int scheduleId = 8;
        var dispatcher = new RecordingDispatcher();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([]),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            dispatcher,
            CreateInjection(prepare),
            new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackStartedAction);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_prepares_tracks_and_starts_playback()
    {
        const int scheduleId = 42;
        var dispatcher = new RecordingDispatcher();
        var player = new RecordingAudioPlayer();
        var track = CreatePreparedTrack(scheduleId);
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([track]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            dispatcher,
            CreateInjection(prepare, new MinimalPlaylistService()),
            new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackStartedAction);
        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackStatusChangedAction);
        Assert.Equal(1, player.PrepareCallCount);
        Assert.Equal(1, player.PlayCallCount);
    }

    private sealed class PlaybackExplicitStopSink : IDisposable
    {
        public int Count { get; private set; }

        public PlaybackExplicitStopSink() =>
            WeakReferenceMessenger.Default.Register<PlaybackExplicitStopMessage>(this, (_, _) => Count++);

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<PlaybackExplicitStopMessage>(this);
    }

    [Fact]
    public void IsAlarmPlaybackSession_false_before_alarm_playback()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        Assert.False(sut.IsAlarmPlaybackSession);
    }

    [Fact]
    public async Task StopAsync_sends_playback_explicit_stop_message()
    {
        using var sink = new PlaybackExplicitStopSink();
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        await sut.StopAsync();

        Assert.Equal(1, sink.Count);
    }

    [Fact]
    public async Task PauseAsync_when_idle_completes_without_throw()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = await Record.ExceptionAsync(() => sut.PauseAsync());

        Assert.Null(ex);
    }

    [Fact]
    public async Task PlayAsync_when_playlist_empty_completes_without_throw()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = await Record.ExceptionAsync(() => sut.PlayAsync());

        Assert.Null(ex);
    }

    [Fact]
    public void Receive_PlayButtonPressedMessage_with_default_schedule_does_not_throw()
    {
        var dispatcher = new RecordingDispatcher();
        const int scheduleId = 55;
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>(null),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState { DefaultScheduleId = scheduleId }),
            dispatcher,
            CreateInjection(prepare),
            new ScheduleReturningAlarmService(scheduleId));

        using var done = new ManualResetEventSlim(false);
        sut.Receive(new PlayButtonPressedMessage());
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            done.Set();
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Dispose_can_be_called_twice_without_throw()
    {
        var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public async Task SeekForwardAsync_SeekBackwardAsync_and_SeekToAsync_complete_when_idle()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        await sut.SeekForwardAsync();
        await sut.SeekBackwardAsync();
        var ex = await Record.ExceptionAsync(() => sut.SeekToAsync(TimeSpan.FromSeconds(30)));

        Assert.Null(ex);
    }

    [Fact]
    public async Task StopForTeardownAsync_completes_without_throw()
    {
        using var sut = CreateSut(new RecordingAudioPlayer(), new FakePlaybackState(new PlaybackState()));

        var ex = await Record.ExceptionAsync(() => sut.StopForTeardownAsync());

        Assert.Null(ex);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_alarm_sets_alarm_playback_session()
    {
        const int scheduleId = 77;
        var track = CreatePreparedTrack(scheduleId);
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([track]),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: true);

        Assert.True(sut.IsAlarmPlaybackSession);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_same_schedule_while_paused_resumes_without_second_prepare()
    {
        const int scheduleId = 88;
        var player = new RecordingAudioPlayer();
        var track = CreatePreparedTrack(scheduleId);
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([track]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        player.Status = PlayStatus.Paused;

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        Assert.Equal(1, player.PrepareCallCount);
        Assert.True(player.PlayCallCount >= 1);
    }

    [Fact]
    public async Task PlayNextAsync_after_two_track_prepare_prepares_second_track()
    {
        const int scheduleId = 91;
        var player = new RecordingAudioPlayer();
        var first = CreatePreparedTrack(scheduleId);
        var second = CreatePreparedTrack(scheduleId);
        second.PlayItem.Metadata.TrackCode = "2";
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([first, second]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        await sut.PlayNextAsync();

        Assert.Equal(2, player.PrepareCallCount);
    }

    [Fact]
    public async Task ResetAndRetryAsync_reprepares_schedule()
    {
        const int scheduleId = 92;
        var player = new RecordingAudioPlayer();
        var prepareCalls = 0;
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) =>
            {
                prepareCalls++;
                return Task.FromResult<List<AudioPlayerTrack>?>(prepareCalls >= 2 ? [CreatePreparedTrack(scheduleId)] : null);
            },
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        await sut.ResetAndRetryAsync(scheduleId);

        Assert.True(prepareCalls >= 2);
        Assert.True(player.PlayCallCount >= 1);
    }

    [Fact]
    public void Receive_TogglePlayPauseMessage_when_stopped_does_not_throw()
    {
        var player = new RecordingAudioPlayer { Status = PlayStatus.Stopped };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState { Status = PlayStatus.Stopped, DefaultScheduleId = 0 }),
            injection: CreateInjection());

        var ex = Record.Exception(() => sut.Receive(new TogglePlayPauseMessage()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task MediaEnded_after_prepare_does_not_throw()
    {
        const int scheduleId = 93;
        var player = new RecordingAudioPlayer();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        var ex = Record.Exception(() => player.RaiseMediaEnded());

        Assert.Null(ex);
    }

    [Fact]
    public async Task PlayPreviousAsync_with_single_track_playlist_does_not_throw()
    {
        const int scheduleId = 94;
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        var ex = await Record.ExceptionAsync(() => sut.PlayPreviousAsync());

        Assert.Null(ex);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_when_prepare_throws_wraps_in_InvalidOperationException()
    {
        const int scheduleId = 95;
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => throw new HttpRequestException("offline"),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.PrepareAndPlayAsync(scheduleId, isAlarm: false));

        Assert.Contains("95", ex.Message, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task PrepareAndPlayAsync_alarm_with_null_prepare_invokes_fallback_sound()
    {
        const int scheduleId = 96;
        var fallback = new RecordingFallbackAlarmSoundService
        {
            TrackToReturn = CreatePreparedTrack(scheduleId),
        };
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>(null),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare, fallback: fallback),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: true);

        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task PauseAsync_after_prepare_invokes_player_pause()
    {
        const int scheduleId = 97;
        var player = new RecordingAudioPlayer();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        await sut.PauseAsync();

        Assert.Equal(1, player.PauseCallCount);
    }

    [Fact]
    public async Task SeekToAsync_after_prepare_invokes_player_seek()
    {
        const int scheduleId = 98;
        var player = new RecordingAudioPlayer();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        await sut.SeekToAsync(TimeSpan.FromSeconds(45));

        Assert.Equal(1, player.SeekCallCount);
    }

    [Fact]
    public async Task StopAsync_after_prepare_dispatches_playback_stopped()
    {
        const int scheduleId = 99;
        var dispatcher = new RecordingDispatcher();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            dispatcher,
            CreateInjection(prepare),
            new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        await sut.StopAsync();

        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackStoppedAction);
    }

    [Fact]
    public async Task PlayAsync_after_prepare_when_status_stopped_does_not_throw()
    {
        const int scheduleId = 100;
        var player = new RecordingAudioPlayer();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        player.Status = PlayStatus.Stopped;

        var ex = await Record.ExceptionAsync(() => sut.PlayAsync());

        Assert.Null(ex);
        Assert.True(player.PlayCallCount >= 1);
    }

    [Fact]
    public async Task MediaFailed_after_prepare_does_not_throw()
    {
        const int scheduleId = 101;
        var player = new RecordingAudioPlayer();
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        var ex = Record.Exception(() => player.RaiseMediaFailed());

        Assert.Null(ex);
    }

    [Fact]
    public async Task Receive_NextButtonPressedMessage_after_prepare_advances_track()
    {
        const int scheduleId = 102;
        var player = new RecordingAudioPlayer();
        var first = CreatePreparedTrack(scheduleId);
        var second = CreatePreparedTrack(scheduleId);
        second.PlayItem.Metadata.TrackCode = "2";
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([first, second]),
        };
        using var sut = CreateSut(
            player,
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);
        sut.Receive(new NextButtonPressedMessage());

        Assert.True(WaitForPrepareCount(player, expected: 2, TimeSpan.FromSeconds(5)));
    }

    private static bool WaitForPrepareCount(RecordingAudioPlayer player, int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (player.PrepareCallCount >= expected)
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    [Fact]
    public async Task IsAlarmPlaybackSession_false_after_non_alarm_prepare()
    {
        const int scheduleId = 103;
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        Assert.False(sut.IsAlarmPlaybackSession);
    }

    [Fact]
    public async Task SeekForwardAsync_after_prepare_does_not_throw()
    {
        const int scheduleId = 104;
        var prepare = new StubPreparePlaybackService
        {
            PrepareTracksImpl = (_, _) => Task.FromResult<List<AudioPlayerTrack>?>([CreatePreparedTrack(scheduleId)]),
        };
        using var sut = CreateSut(
            new RecordingAudioPlayer(),
            new FakePlaybackState(new PlaybackState()),
            injection: CreateInjection(prepare),
            alarmSchedule: new ScheduleReturningAlarmService(scheduleId));

        await sut.PrepareAndPlayAsync(scheduleId, isAlarm: false);

        var ex = await Record.ExceptionAsync(() => sut.SeekForwardAsync());

        Assert.Null(ex);
    }
}
