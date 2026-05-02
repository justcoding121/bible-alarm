#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ProgressTrackerTests
{
    private sealed class RecordingPlaylist : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack = new() { TrackCode = "1", Title = "T" };
        private static readonly BiblePublicationSection DummySection = new() { SectionCode = "s", Name = "S" };

        public List<TrackMetadata> MarkedAsPlayed { get; } = [];
        public List<TrackMetadata> MarkedAsFinished { get; } = [];

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
        {
            MarkedAsPlayed.Add(trackMetadata);
            return Task.CompletedTask;
        }

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata)
        {
            MarkedAsFinished.Add(trackMetadata);
            return Task.CompletedTask;
        }

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
        public PlayStatus Status { get; set; } = PlayStatus.Stopped;

        public TimeSpan? CurrentPosition { get; set; }

        public TimeSpan Duration => TimeSpan.Zero;

        public bool IsActuallyPlayingOrPaused => false;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;

        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067

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

        public void Dispose()
        {
        }
    }

    private static TrackMetadata BibleMeta() =>
        new()
        {
            ScheduleId = 50,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "2",
            LookUpPath = "/b",
        };

    private static TrackMetadata MusicMeta() =>
        new()
        {
            ScheduleId = 51,
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "osg",
            TrackCode = "3",
            LookUpPath = "/m",
        };

    [Fact]
    public async Task SaveProgressAsync_no_ops_when_playlist_invalid()
    {
        var playlist = new RecordingPlaylist();
        var audio = new StubAudioPlayer();
        using var sut = new ProgressTracker(playlist, audio, TestLogging.CreateLogger());
        var meta = BibleMeta();
        var list = new List<AudioPlayerTrack> { new() { PlayItem = new PlayItem(meta, "u") } };

        await sut.SaveProgressAsync(null, 0);
        await sut.SaveProgressAsync(list, -1);
        await sut.SaveProgressAsync(list, 5);

        Assert.Empty(playlist.MarkedAsPlayed);
        Assert.Empty(playlist.MarkedAsFinished);
    }

    [Fact]
    public async Task SaveProgressAsync_music_marks_finished_once_while_playing_with_position()
    {
        var playlist = new RecordingPlaylist();
        var audio = new StubAudioPlayer
        {
            Status = PlayStatus.Playing,
            CurrentPosition = TimeSpan.FromSeconds(2),
        };
        using var sut = new ProgressTracker(playlist, audio, TestLogging.CreateLogger());
        var list = new List<AudioPlayerTrack> { new() { PlayItem = new PlayItem(MusicMeta(), "u") } };

        await sut.SaveProgressAsync(list, 0);
        await sut.SaveProgressAsync(list, 0);

        Assert.Single(playlist.MarkedAsFinished);
        Assert.Empty(playlist.MarkedAsPlayed);
    }

    [Fact]
    public async Task SaveProgressAsync_bible_persists_while_playing_when_position_known()
    {
        var playlist = new RecordingPlaylist();
        var audio = new StubAudioPlayer
        {
            Status = PlayStatus.Playing,
            CurrentPosition = TimeSpan.FromSeconds(11),
        };
        using var sut = new ProgressTracker(playlist, audio, TestLogging.CreateLogger());
        var meta = BibleMeta();
        var list = new List<AudioPlayerTrack> { new() { PlayItem = new PlayItem(meta, "u") } };

        await sut.SaveProgressAsync(list, 0);

        Assert.Single(playlist.MarkedAsPlayed);
        Assert.Equal(TimeSpan.FromSeconds(11), meta.FinishedDuration);
    }

    [Fact]
    public async Task SaveProgressAsync_bible_skips_when_stopped_without_force()
    {
        var playlist = new RecordingPlaylist();
        var audio = new StubAudioPlayer { Status = PlayStatus.Stopped };
        using var sut = new ProgressTracker(playlist, audio, TestLogging.CreateLogger());
        var list = new List<AudioPlayerTrack> { new() { PlayItem = new PlayItem(BibleMeta(), "u") } };

        await sut.SaveProgressAsync(list, 0);

        Assert.Empty(playlist.MarkedAsPlayed);
    }

    [Fact]
    public async Task SaveProgressAsync_bible_on_pause_uses_in_memory_duration_once()
    {
        var playlist = new RecordingPlaylist();
        var audio = new StubAudioPlayer { Status = PlayStatus.Paused, CurrentPosition = null };
        using var sut = new ProgressTracker(playlist, audio, TestLogging.CreateLogger());
        var meta = BibleMeta();
        meta.FinishedDuration = TimeSpan.FromMinutes(3);
        var list = new List<AudioPlayerTrack> { new() { PlayItem = new PlayItem(meta, "u") } };

        await sut.SaveProgressAsync(list, 0);

        Assert.Single(playlist.MarkedAsPlayed);
    }
}
