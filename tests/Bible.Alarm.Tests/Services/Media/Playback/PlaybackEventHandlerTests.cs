#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackEventHandlerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class NullPlaylistService : IPlaylistService
    {
        private static TrackMetadata DummyMeta() =>
            new()
            {
                ScheduleId = 1,
                LanguageCode = "E",
                PublicationCode = "nwt",
                TrackCode = "1",
            };

        private static PlayItem DummyPlayItem() => new(DummyMeta(), "https://example.invalid/track");

        private static TrackNavigationResult DummyNav() =>
            new("nwt", null, new BiblePublicationTrack { TrackCode = "1" });

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) => Task.FromResult(DummyPlayItem());

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) => Task.FromResult<PlayItem?>(DummyPlayItem());

        public Task<List<PlayItem>> NextTracks(int scheduleId) => Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(DummyNav());

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(DummyNav());

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(DummyPlayItem());

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(DummyPlayItem());

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) => Task.CompletedTask;
    }

    private sealed class StubCdnProbe : ICdnPlaybackUrlProbe
    {
        public Task<CdnUrlProbeOutcome> ProbeStreamingUrlAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(CdnUrlProbeOutcome.Indeterminate);
    }

    private sealed class StubTrackCdnRefresher : ITrackCdnUrlRefresher
    {
        public string? UrlToReturn { get; set; }
        public int RefreshCallCount { get; private set; }

        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata, CancellationToken cancellationToken = default)
        {
            RefreshCallCount++;
            return Task.FromResult(UrlToReturn);
        }
    }

    private sealed class ConfigurableCdnProbe : ICdnPlaybackUrlProbe
    {
        public CdnUrlProbeOutcome Outcome { get; set; } = CdnUrlProbeOutcome.Indeterminate;

        public Task<CdnUrlProbeOutcome> ProbeStreamingUrlAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(Outcome);
    }

    private sealed class RecordingPlaylistService : IPlaylistService
    {
        public int MarkFinishedCount { get; private set; }
        public int PersistPointerCount { get; private set; }

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata)
        {
            MarkFinishedCount++;
            return Task.CompletedTask;
        }

        public Task<PlayItem> NextTrack(int scheduleId) => Task.FromResult(new PlayItem(BibleMeta(), "https://x"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) => Task.FromResult<PlayItem?>(null);

        public Task<List<PlayItem>> NextTracks(int scheduleId) => Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult("nwt", null, new BiblePublicationTrack { TrackCode = "1" }));

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult("nwt", null, new BiblePublicationTrack { TrackCode = "1" }));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(BibleMeta(), "https://x"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(BibleMeta(), "https://x"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata)
        {
            PersistPointerCount++;
            return Task.CompletedTask;
        }
    }

    private static TrackMetadata BibleMeta(int scheduleId = 1, string trackCode = "1") =>
        new()
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = trackCode,
        };

    private static AudioPlayerTrack Track(TrackMetadata meta, string uri = "https://cdn.example/a.mp3") =>
        new() { Uri = uri, PlayItem = new PlayItem(meta, uri) };

    private static PlaybackEventHandler CreateHandler(
        RecordingDispatcher dispatcher,
        IPlaylistService? playlist = null,
        ICdnPlaybackUrlProbe? probe = null,
        StubTrackCdnRefresher? refresher = null) =>
        new(
            playlist ?? new NullPlaylistService(),
            dispatcher,
            TestLogging.CreateLogger(),
            new PlaybackNavigationManager(dispatcher),
            probe ?? new StubCdnProbe(),
            refresher ?? new StubTrackCdnRefresher());

    private sealed class IndexHolder
    {
        public int Value { get; set; }
    }

    private static PlaybackMediaEndedRequest EndedRequest(
        List<AudioPlayerTrack> playlist,
        IndexHolder indexHolder,
        bool indefinite = false,
        Func<Task<bool>>? tryAppend = null,
        int? scheduleId = 1,
        bool manualNavPending = false,
        Func<bool, Task>? play = null,
        TaskCompletionSource? stopSignal = null) =>
        new(
            Playlist: playlist,
            GetCurrentTrackIndex: () => indexHolder.Value,
            SetCurrentTrackIndex: i => indexHolder.Value = i,
            CurrentScheduleId: scheduleId,
            IsIndefinitePlayback: indefinite,
            TryAppendNextTrackAsync: tryAppend ?? (() => Task.FromResult(false)),
            PlayCurrentTrackAsync: play ?? (_ => Task.CompletedTask),
            StopAsyncInternal: _ =>
            {
                stopSignal?.TrySetResult();
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => manualNavPending,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) => Task.CompletedTask);

    [Fact]
    public async Task HandleMediaEndedAsync_ReturnsEarly_When_ManualNavigationPending()
    {
        var dispatcher = new RecordingDispatcher();
        var navigationManager = new PlaybackNavigationManager(dispatcher);
        var sut = new PlaybackEventHandler(
            new NullPlaylistService(),
            dispatcher,
            TestLogging.CreateLogger(),
            navigationManager,
            new StubCdnProbe(),
            new StubTrackCdnRefresher());

        var playCalls = 0;
        var stopCalls = 0;

        await sut.HandleMediaEndedAsync(new PlaybackMediaEndedRequest(
            Playlist: [],
            GetCurrentTrackIndex: () => 0,
            SetCurrentTrackIndex: _ => { },
            CurrentScheduleId: 1,
            IsIndefinitePlayback: false,
            TryAppendNextTrackAsync: () => Task.FromResult(false),
            PlayCurrentTrackAsync: _ =>
            {
                playCalls++;
                return Task.CompletedTask;
            },
            StopAsyncInternal: _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => true,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) => Task.CompletedTask));

        Assert.Equal(0, playCalls);
        Assert.Equal(0, stopCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleMediaEndedAsync_advances_to_next_in_playlist_and_dispatches_auto_advancing()
    {
        var dispatcher = new RecordingDispatcher();
        var playlist = new List<AudioPlayerTrack>
        {
            Track(BibleMeta(trackCode: "1")),
            Track(BibleMeta(trackCode: "2")),
        };
        var index = new IndexHolder();
        var playCalls = 0;
        var sut = CreateHandler(dispatcher);

        await sut.HandleMediaEndedAsync(EndedRequest(
            playlist,
            index,
            play: _ =>
            {
                playCalls++;
                return Task.CompletedTask;
            }));

        Assert.Equal(1, index.Value);
        Assert.Equal(1, playCalls);
        Assert.Contains(dispatcher.Dispatched, a => a is SetAutoAdvancingAction);
        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackTrackTransitionStartedAction);
    }

    [Fact]
    public async Task HandleMediaEndedAsync_stops_after_last_finite_track()
    {
        using var stopping = new CountingBeginStoppingRecipient();
        var dispatcher = new RecordingDispatcher();
        var playlist = new List<AudioPlayerTrack> { Track(BibleMeta()) };
        var index = new IndexHolder();
        var stopStarted = new TaskCompletionSource();
        var sut = CreateHandler(dispatcher);

        await sut.HandleMediaEndedAsync(EndedRequest(playlist, index, stopSignal: stopStarted));

        await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, stopping.Count);
    }

    public sealed class CountingBeginStoppingRecipient : IRecipient<BeginStoppingPlaybackMessage>, IDisposable
    {
        public int Count { get; private set; }

        public CountingBeginStoppingRecipient() =>
            WeakReferenceMessenger.Default.Register<BeginStoppingPlaybackMessage>(this);

        public void Receive(BeginStoppingPlaybackMessage message) => Count++;

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<BeginStoppingPlaybackMessage>(this);
    }

    [Fact]
    public async Task HandleMediaEndedAsync_indefinite_last_track_appends_and_plays_next()
    {
        var dispatcher = new RecordingDispatcher();
        var playlist = new List<AudioPlayerTrack> { Track(BibleMeta()) };
        var index = new IndexHolder();
        var playCalls = 0;
        var sut = CreateHandler(dispatcher, new RecordingPlaylistService());

        await sut.HandleMediaEndedAsync(EndedRequest(
            playlist,
            index,
            indefinite: true,
            tryAppend: () =>
            {
                playlist.Add(Track(BibleMeta(trackCode: "2")));
                return Task.FromResult(true);
            },
            play: _ =>
            {
                playCalls++;
                return Task.CompletedTask;
            }));

        Assert.Equal(1, index.Value);
        Assert.Equal(1, playCalls);
    }

    [Fact]
    public async Task HandleMediaEndedAsync_indefinite_append_failure_shows_error_message()
    {
        var dispatcher = new RecordingDispatcher();
        var playlist = new List<AudioPlayerTrack> { Track(BibleMeta(scheduleId: 9)) };
        var index = 0;
        string? errorMessage = null;
        var sut = CreateHandler(dispatcher, new RecordingPlaylistService());

        await sut.HandleMediaEndedAsync(new PlaybackMediaEndedRequest(
            Playlist: playlist,
            GetCurrentTrackIndex: () => index,
            SetCurrentTrackIndex: i => index = i,
            CurrentScheduleId: 9,
            IsIndefinitePlayback: true,
            TryAppendNextTrackAsync: () => Task.FromResult(false),
            PlayCurrentTrackAsync: _ => Task.CompletedTask,
            StopAsyncInternal: _ => Task.CompletedTask,
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (msg, _) =>
            {
                errorMessage = msg;
                return Task.CompletedTask;
            }));

        Assert.Equal(AppConstants.Media.PlaybackModalMessages.CouldNotLoadNextPartCheckConnectionTapRetry, errorMessage);
    }

    [Fact]
    public async Task HandleMediaFailedAsync_returns_early_when_manual_navigation_pending()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = CreateHandler(dispatcher);
        var errorShown = false;

        await sut.HandleMediaFailedAsync(new PlaybackMediaFailedRequest(
            Playlist: [],
            GetCurrentTrackIndex: () => 0,
            TrackUri: "file://local",
            TrackUrl: "file://local",
            PlayCurrentTrackAsync: _ => Task.CompletedTask,
            GetIsManualNavigationPending: () => true,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) =>
            {
                errorShown = true;
                return Task.CompletedTask;
            },
            IsPlaybackEstablishedForTrack: _ => false));

        Assert.False(errorShown);
    }

    [Fact]
    public async Task HandleMediaFailedAsync_shows_generic_retry_for_local_file_failure()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = CreateHandler(dispatcher);
        string? message = null;
        var meta = BibleMeta();
        var playlist = new List<AudioPlayerTrack> { Track(meta, "file:///cache/track.mp3") };

        await sut.HandleMediaFailedAsync(new PlaybackMediaFailedRequest(
            Playlist: playlist,
            GetCurrentTrackIndex: () => 0,
            TrackUri: "file:///cache/track.mp3",
            TrackUrl: "file:///cache/track.mp3",
            PlayCurrentTrackAsync: _ => Task.CompletedTask,
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (msg, _) =>
            {
                message = msg;
                return Task.CompletedTask;
            },
            IsPlaybackEstablishedForTrack: _ => false));

        Assert.Equal(AppConstants.Media.PlaybackModalMessages.PlaybackFailedTapRetry, message);
    }

    [Fact]
    public async Task HandleMediaFailedAsync_refreshes_stale_cdn_and_replays_once()
    {
        var dispatcher = new RecordingDispatcher();
        var probe = new ConfigurableCdnProbe { Outcome = CdnUrlProbeOutcome.NotFoundOrGone };
        var refresher = new StubTrackCdnRefresher { UrlToReturn = "https://cdn.example/refreshed.mp3" };
        var sut = CreateHandler(dispatcher, probe: probe, refresher: refresher);
        var playCount = 0;
        var meta = BibleMeta();
        var item = new PlayItem(meta, "https://cdn.example/old.mp3");
        var track = new AudioPlayerTrack { Uri = "https://cdn.example/old.mp3", PlayItem = item };

        await sut.HandleMediaFailedAsync(new PlaybackMediaFailedRequest(
            Playlist: [track],
            GetCurrentTrackIndex: () => 0,
            TrackUri: "https://cdn.example/old.mp3",
            TrackUrl: "https://cdn.example/old.mp3",
            PlayCurrentTrackAsync: _ =>
            {
                playCount++;
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) => Task.CompletedTask,
            IsPlaybackEstablishedForTrack: _ => false));

        Assert.Equal(1, playCount);
        Assert.Equal("https://cdn.example/refreshed.mp3", item.Url);
        Assert.True(item.CdnStaleUrlRefetchReplayIssued);
    }

    [Fact(Skip = "Open-phase silent retry depends on CDN probe + streaming URL classification not fully stubbed here.")]
    public async Task HandleMediaFailedAsync_open_phase_silent_retry_before_showing_modal()
    {
        var dispatcher = new RecordingDispatcher();
        var probe = new ConfigurableCdnProbe { Outcome = CdnUrlProbeOutcome.ResourceReachable };
        var sut = CreateHandler(dispatcher, probe: probe);
        var playCount = 0;
        var errorShown = false;
        var item = new PlayItem(BibleMeta(), "https://cdn.example/stream.mp3");
        var track = new AudioPlayerTrack { Uri = "https://cdn.example/stream.mp3", PlayItem = item };

        await sut.HandleMediaFailedAsync(new PlaybackMediaFailedRequest(
            Playlist: [track],
            GetCurrentTrackIndex: () => 0,
            TrackUri: "https://cdn.example/stream.mp3",
            TrackUrl: "https://cdn.example/stream.mp3",
            PlayCurrentTrackAsync: _ =>
            {
                playCount++;
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) =>
            {
                errorShown = true;
                return Task.CompletedTask;
            },
            IsPlaybackEstablishedForTrack: _ => false));

        Assert.Equal(1, playCount);
        Assert.False(errorShown);
        Assert.True(item.StreamingOpenPhaseMediaFailedRetryDone);
    }

    [Fact]
    public async Task HandleMediaFailedAsync_uses_connection_lost_message_when_playback_had_started()
    {
        var dispatcher = new RecordingDispatcher();
        var probe = new ConfigurableCdnProbe { Outcome = CdnUrlProbeOutcome.ResourceReachable };
        var sut = CreateHandler(dispatcher, probe: probe);
        string? message = null;
        var track = Track(BibleMeta(), "https://cdn.example/live.mp3");

        await sut.HandleMediaFailedAsync(new PlaybackMediaFailedRequest(
            Playlist: [track],
            GetCurrentTrackIndex: () => 0,
            TrackUri: "https://cdn.example/live.mp3",
            TrackUrl: "https://cdn.example/live.mp3",
            PlayCurrentTrackAsync: _ => Task.CompletedTask,
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (msg, _) =>
            {
                message = msg;
                return Task.CompletedTask;
            },
            IsPlaybackEstablishedForTrack: _ => true));

        Assert.Equal(AppConstants.Media.PlaybackModalMessages.PlaybackStoppedConnectionLostTapRetry, message);
    }
}
