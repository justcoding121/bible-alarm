#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackResetExecutorTests
{
    private sealed class PlaylistStub : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack = new() { TrackCode = "1", Title = "T" };
        private static readonly BiblePublicationSection DummySection = new() { SectionCode = "s", Name = "S" };

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(DummyMeta(), "https://stub"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(new PlayItem(DummyMeta(), "https://stub"));

        public Task<List<PlayItem>> NextTracks(int scheduleId) =>
            Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult(publicationCode, null, DummyTrack));

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
            string publicationCode,
            string? sectionCode, string trackCode) =>
            Task.FromResult(new TrackNavigationResult(publicationCode, null, DummyTrack));

        public Task<KeyValuePair<string, BiblePublicationSection>>
            GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata DummyMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "1",
                LookUpPath = "/stub",
            };
    }

    private sealed class StubAudioPlayer : IAudioPlayer
    {
        public int ResetAsyncCalls { get; private set; }

        public PlayStatus Status { get; private set; } = PlayStatus.Stopped;

        public TimeSpan Duration => TimeSpan.Zero;

        public TimeSpan? CurrentPosition => null;

        public bool IsActuallyPlayingOrPaused => false;

        public event EventHandler<EventArgs>? MediaEnded;

        public event EventHandler<EventArgs>? MediaFailed;

        public void Dispose()
        {
        }

        public Task PrepareAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task ResetAsync()
        {
            ResetAsyncCalls++;
            return Task.CompletedTask;
        }

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task SetMutedAsync(bool muted) => Task.CompletedTask;

        public void NotifyTrackTransitionStarting()
        {
        }

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;
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

    [Fact]
    public async Task ResetAsync_stops_progress_resets_player_state_and_dispatches_stopped()
    {
        var logger = TestLogging.CreateLogger();
        var audio = new StubAudioPlayer();
        using var progressTracker = new ProgressTracker(new PlaylistStub(), audio, logger);
        var stateManager = new PlaybackStateManager(logger)
        {
            CurrentScheduleId = 99,
            CurrentTrackIndex = 3,
        };
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaybackResetExecutor(progressTracker, audio, stateManager, dispatcher, logger);

        await sut.ResetAsync();

        Assert.Equal(1, audio.ResetAsyncCalls);
        Assert.Null(stateManager.CurrentScheduleId);
        Assert.Equal(-1, stateManager.CurrentTrackIndex);
        Assert.Contains(dispatcher.Dispatched, x => x is PlaybackStoppedAction);
    }

    [Fact]
    public async Task ResetStateForRetryAsync_dispatches_clear_error_and_resets_core_state()
    {
        var logger = TestLogging.CreateLogger();
        var audio = new StubAudioPlayer();
        using var progressTracker = new ProgressTracker(new PlaylistStub(), audio, logger);
        var stateManager = new PlaybackStateManager(logger) { CurrentScheduleId = 1 };
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaybackResetExecutor(progressTracker, audio, stateManager, dispatcher, logger);

        await sut.ResetStateForRetryAsync();

        var error = dispatcher.Dispatched.OfType<PlaybackErrorAction>().Single();
        Assert.Null(error.ErrorMessage);
        Assert.Equal(1, audio.ResetAsyncCalls);
        Assert.Null(stateManager.CurrentScheduleId);
    }
}
