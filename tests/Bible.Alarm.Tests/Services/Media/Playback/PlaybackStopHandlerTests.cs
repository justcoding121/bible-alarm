#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStopHandlerTests
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
        public int StopCalls { get; private set; }
        public int ResetCalls { get; private set; }

        public Task PrepareAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public Task PlayAsync() => Task.CompletedTask;

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StopAsync()
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        public Task ResetAsync()
        {
            ResetCalls++;
            return Task.CompletedTask;
        }

        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;

        public Task SetMutedAsync(bool muted) => Task.CompletedTask;

        public void NotifyTrackTransitionStarting()
        {
        }

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public TimeSpan? CurrentPosition => null;

        public TimeSpan Duration => TimeSpan.Zero;

        public PlayStatus Status => PlayStatus.Stopped;

        public bool IsActuallyPlayingOrPaused => false;

        public event EventHandler<EventArgs>? MediaEnded
        {
            add { }
            remove { }
        }

        public event EventHandler<EventArgs>? MediaFailed
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingPlaylistService : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack =
            new() { TrackCode = "1", Title = "T" };

        private static readonly BiblePublicationSection DummySection =
            new() { SectionCode = "s", Name = "S" };

        public List<TrackMetadata> MarkedAsPlayed { get; } = [];

        public List<int> SavedScheduleIds { get; } = [];

        private static TrackMetadata DummyMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "1",
            };

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
        {
            MarkedAsPlayed.Add(trackMetadata);
            return Task.CompletedTask;
        }

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(DummyMeta(), "https://stub"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(new PlayItem(DummyMeta(), "https://stub"));

        public Task<List<PlayItem>> NextTracks(int scheduleId) =>
            Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId)
        {
            SavedScheduleIds.Add(currentScheduleId);
            return Task.CompletedTask;
        }

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
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;
    }

    private static TrackMetadata BibleMeta() =>
        new()
        {
            ScheduleId = 7,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "01",
        };

    private static TrackMetadata MusicMeta() =>
        new()
        {
            ScheduleId = 7,
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "osg",
            TrackCode = "1",
        };

    [Fact]
    public async Task StopAsync_dispatches_PlaybackStoppedAction_when_not_skipped()
    {
        var dispatcher = new RecordingDispatcher();
        var playlists = new RecordingPlaylistService();
        var audio = new StubAudioPlayer();
        var sut = new PlaybackStopHandler(audio, playlists, dispatcher, TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            ScheduleIdToSave: null,
            TrackMetadataToMark: null,
            SkipMarkAsPlayed: false,
            SkipSaveLastPlayed: false,
            PreparationCancellationTokenSource: null,
            ResetState: () => { },
            StopProgressTimer: () => { },
            SkipDispatchStopped: false));

        Assert.Single(dispatcher.Dispatched.OfType<PlaybackStoppedAction>());
        Assert.Equal(1, audio.StopCalls);
        Assert.Equal(1, audio.ResetCalls);
        Assert.Empty(playlists.MarkedAsPlayed);
        Assert.Empty(playlists.SavedScheduleIds);
    }

    [Fact]
    public async Task StopAsync_skips_PlaybackStoppedAction_when_skip_dispatch()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new PlaybackStopHandler(new StubAudioPlayer(), new RecordingPlaylistService(),
            dispatcher, TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            null,
            null,
            false,
            false,
            null,
            () => { },
            () => { },
            SkipDispatchStopped: true));

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task StopAsync_marks_non_music_track_played_when_configured()
    {
        var meta = BibleMeta();
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackStopHandler(new StubAudioPlayer(), playlists, new RecordingDispatcher(),
            TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            null,
            TrackMetadataToMark: meta,
            SkipMarkAsPlayed: false,
            SkipSaveLastPlayed: false,
            null,
            () => { },
            () => { }));

        Assert.Same(meta, Assert.Single(playlists.MarkedAsPlayed));
    }

    [Fact]
    public async Task StopAsync_skips_mark_for_music_PlayType_even_when_tracking_metadata_provided()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackStopHandler(new StubAudioPlayer(), playlists, new RecordingDispatcher(),
            TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            null,
            MusicMeta(),
            false,
            false,
            null,
            () => { },
            () => { }));

        Assert.Empty(playlists.MarkedAsPlayed);
    }

    [Fact]
    public async Task StopAsync_saves_last_played_when_requested()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackStopHandler(new StubAudioPlayer(), playlists, new RecordingDispatcher(),
            TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            ScheduleIdToSave: 42,
            null,
            false,
            SkipSaveLastPlayed: false,
            null,
            () => { },
            () => { }));

        Assert.Equal(42, Assert.Single(playlists.SavedScheduleIds));
    }

    [Fact]
    public async Task StopAsync_skips_save_last_played_when_flag_set()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackStopHandler(new StubAudioPlayer(), playlists, new RecordingDispatcher(),
            TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            42,
            null,
            false,
            SkipSaveLastPlayed: true,
            null,
            () => { },
            () => { }));

        Assert.Empty(playlists.SavedScheduleIds);
    }

    [Fact]
    public async Task StopAsync_invokes_reset_and_progress_hooks()
    {
        var resets = 0;
        var stops = 0;
        var sut = new PlaybackStopHandler(new StubAudioPlayer(), new RecordingPlaylistService(),
            new RecordingDispatcher(), TestLogging.CreateLogger());

        await sut.StopAsync(new PlaybackStopRequest(
            null,
            null,
            false,
            false,
            null,
            () => resets++,
            () => stops++,
            SkipDispatchStopped: true));

        Assert.Equal(1, resets);
        Assert.Equal(1, stops);
    }

    [Fact]
    public async Task StopAsync_best_effort_cancels_preparation_when_cts_provided()
    {
        using var cts = new CancellationTokenSource();

        await new PlaybackStopHandler(new StubAudioPlayer(), new RecordingPlaylistService(),
                new RecordingDispatcher(), TestLogging.CreateLogger())
            .StopAsync(new PlaybackStopRequest(
                null,
                null,
                false,
                false,
                PreparationCancellationTokenSource: cts,
                () => { },
                () => { },
                SkipDispatchStopped: true));

        Assert.True(cts.Token.IsCancellationRequested);
    }
}
