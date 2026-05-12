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

public sealed class PlaybackNavigationHandlerTests
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

    private sealed class StubPlaylistService : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack =
            new() { TrackCode = "1", Title = "T" };

        private static readonly BiblePublicationSection DummySection =
            new() { SectionCode = "s", Name = "S" };

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
            string? sectionCode,
            string trackCode) =>
            Task.FromResult(new TrackNavigationResult(publicationCode, null, DummyTrack));

        public Task<KeyValuePair<string, BiblePublicationSection>>
            GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode,
            string sectionCode) =>
            Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", DummySection));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;
    }

    private static PlaybackNavigationNextRequest UnusedNextDelegates(
        HashSet<int> visited,
        List<AudioPlayerTrack>? playlist) =>
        new(
            playlist,
            () => throw new Xunit.Sdk.XunitException("GetCurrentTrackIndex should not run for empty navigation."),
            _ => throw new Xunit.Sdk.XunitException("SetCurrentTrackIndex should not run for empty navigation."),
            null,
            false,
            () => Task.FromResult(false),
            visited,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            () => Task.CompletedTask);

    private static PlaybackNavigationPreviousRequest UnusedPrevDelegates(HashSet<int> visited,
        List<AudioPlayerTrack>? playlist) =>
        new(
            playlist,
            () => throw new Xunit.Sdk.XunitException("GetCurrentTrackIndex should not run for empty navigation."),
            _ => throw new Xunit.Sdk.XunitException("SetCurrentTrackIndex should not run for empty navigation."),
            null,
            false,
            () => Task.FromResult(false),
            visited,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

    [Fact]
    public async Task PlayNextAsync_when_playlist_missing_returns_without_dispatch_or_delay()
    {
        using var audio = new StubAudioPlayer();
        var dispatcher = new RecordingDispatcher();
        using var progress = new ProgressTracker(new StubPlaylistService(), audio, TestLogging.CreateLogger());
        var nav = new PlaybackNavigationManager(dispatcher);
        var sut = new PlaybackNavigationHandler(audio, dispatcher, TestLogging.CreateLogger(), progress, nav);

        await sut.PlayNextAsync(UnusedNextDelegates([], null));

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task PlayNextAsync_when_playlist_empty_returns_without_dispatch_or_delay()
    {
        using var audio = new StubAudioPlayer();
        var dispatcher = new RecordingDispatcher();
        using var progress = new ProgressTracker(new StubPlaylistService(), audio, TestLogging.CreateLogger());
        var nav = new PlaybackNavigationManager(dispatcher);
        var sut = new PlaybackNavigationHandler(audio, dispatcher, TestLogging.CreateLogger(), progress, nav);

        await sut.PlayNextAsync(UnusedNextDelegates([], []));

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task PlayPreviousAsync_when_playlist_missing_returns_without_dispatch_or_delay()
    {
        using var audio = new StubAudioPlayer();
        var dispatcher = new RecordingDispatcher();
        using var progress = new ProgressTracker(new StubPlaylistService(), audio, TestLogging.CreateLogger());
        var nav = new PlaybackNavigationManager(dispatcher);
        var sut = new PlaybackNavigationHandler(audio, dispatcher, TestLogging.CreateLogger(), progress, nav);

        await sut.PlayPreviousAsync(UnusedPrevDelegates([], null));

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task PlayPreviousAsync_when_playlist_empty_returns_without_dispatch_or_delay()
    {
        using var audio = new StubAudioPlayer();
        var dispatcher = new RecordingDispatcher();
        using var progress = new ProgressTracker(new StubPlaylistService(), audio, TestLogging.CreateLogger());
        var nav = new PlaybackNavigationManager(dispatcher);
        var sut = new PlaybackNavigationHandler(audio, dispatcher, TestLogging.CreateLogger(), progress, nav);

        await sut.PlayPreviousAsync(UnusedPrevDelegates([], []));

        Assert.Empty(dispatcher.Dispatched);
    }
}
