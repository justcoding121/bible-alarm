#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaEventAdapterTests
{
    internal sealed class CountingBeginStoppingRecipient : IRecipient<BeginStoppingPlaybackMessage>, IDisposable
    {
        public int Count { get; private set; }

        public CountingBeginStoppingRecipient() =>
            WeakReferenceMessenger.Default.Register<BeginStoppingPlaybackMessage>(this);

        public void Receive(BeginStoppingPlaybackMessage message) =>
            Count++;

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<BeginStoppingPlaybackMessage>(this);
    }

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
        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubAudioPlayer : IAudioPlayer
    {
        public PlayStatus Status { get; private set; } = PlayStatus.Stopped;

        public TimeSpan Duration => TimeSpan.Zero;

        public TimeSpan? CurrentPosition => null;

        public bool IsActuallyPlayingOrPaused => false;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;

        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067

        public void Dispose()
        {
        }

        public Task PrepareAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task ResetAsync() => Task.CompletedTask;

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task SetMutedAsync(bool muted) => Task.CompletedTask;

        public void NotifyTrackTransitionStarting()
        {
        }

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;
    }

    private static PlaybackMediaEventAdapter CreateSut(
        PlaybackEventHandler handler,
        ProgressTracker progress,
        PlaybackMediaEventAdapter.Callbacks callbacks)
    {
        return new PlaybackMediaEventAdapter(handler, progress, TestLogging.CreateLogger(), callbacks);
    }

    [Fact]
    public async Task OnMediaEnded_sends_begin_stopping_before_handler_when_last_finite_track_finishes()
    {
        using var recipient = new CountingBeginStoppingRecipient();
        var dispatcher = new RecordingDispatcher();
        var navigationManager = new PlaybackNavigationManager(dispatcher);
        var handler = new PlaybackEventHandler(
            new NullPlaylistService(),
            dispatcher,
            TestLogging.CreateLogger(),
            navigationManager,
            new StubCdnProbe(),
            new StubTrackCdnRefresher());

        using var audio = new StubAudioPlayer();
        using var progress = new ProgressTracker(new NullPlaylistService(), audio, TestLogging.CreateLogger());

        var meta = new TrackMetadata
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "1",
        };
        var track = new AudioPlayerTrack
        {
            Uri = "file://current",
            PlayItem = new PlayItem(meta, "https://play.example/track"),
        };
        var playlist = new List<AudioPlayerTrack> { track };
        var indexBox = 0;
        var stopStarted = new TaskCompletionSource();

        var callbacks = new PlaybackMediaEventAdapter.Callbacks(
            GetPlaylist: () => playlist,
            GetCurrentTrackIndex: () => indexBox,
            SetCurrentTrackIndex: i => indexBox = i,
            GetCurrentScheduleId: () => 1,
            GetIsIndefinitePlayback: () => false,
            TryAppendNextTrackAsync: () => Task.FromResult(false),
            PlayCurrentTrackAsync: _ => Task.CompletedTask,
            StopAsyncInternal: _ =>
            {
                stopStarted.TrySetResult();
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) => Task.CompletedTask,
            IsPlaybackEstablishedForTrack: _ => false);

        var sut = CreateSut(handler, progress, callbacks);

        sut.OnMediaEnded(null, EventArgs.Empty);

        await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(recipient.Count >= 1);
    }

    [Fact]
    public void OnMediaEnded_does_not_send_begin_stopping_while_manual_navigation_is_pending()
    {
        using var recipient = new CountingBeginStoppingRecipient();
        var dispatcher = new RecordingDispatcher();
        var navigationManager = new PlaybackNavigationManager(dispatcher);
        var handler = new PlaybackEventHandler(
            new NullPlaylistService(),
            dispatcher,
            TestLogging.CreateLogger(),
            navigationManager,
            new StubCdnProbe(),
            new StubTrackCdnRefresher());

        using var audio = new StubAudioPlayer();
        using var progress = new ProgressTracker(new NullPlaylistService(), audio, TestLogging.CreateLogger());

        var meta = new TrackMetadata
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            TrackCode = "1",
        };
        var track = new AudioPlayerTrack { Uri = "u", PlayItem = new PlayItem(meta, "https://x") };
        var playlist = new List<AudioPlayerTrack> { track };

        var callbacks = new PlaybackMediaEventAdapter.Callbacks(
            GetPlaylist: () => playlist,
            GetCurrentTrackIndex: () => 0,
            SetCurrentTrackIndex: _ => { },
            GetCurrentScheduleId: () => 1,
            GetIsIndefinitePlayback: () => false,
            TryAppendNextTrackAsync: () => Task.FromResult(false),
            PlayCurrentTrackAsync: _ => Task.CompletedTask,
            StopAsyncInternal: _ => Task.CompletedTask,
            GetIsManualNavigationPending: () => true,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) => Task.CompletedTask,
            IsPlaybackEstablishedForTrack: _ => false);

        var sut = CreateSut(handler, progress, callbacks);

        sut.OnMediaEnded(null, EventArgs.Empty);

        Assert.Equal(0, recipient.Count);
    }
}
