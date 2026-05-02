#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Tests.Support;
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
        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

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
}
