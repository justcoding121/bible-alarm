#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackOperationHandlerTests
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
        public PlayStatus Status { get; set; }
        public bool IsActuallyPlayingOrPaused { get; set; }
        public TimeSpan? CurrentPosition { get; set; }
        public TimeSpan Duration { get; set; }

        public List<string> Operations { get; } = [];
        public List<TimeSpan> SeekTargets { get; } = [];

        public Task PrepareAsync(AudioPlayerTrack track)
        {
            Operations.Add(nameof(PrepareAsync));
            return Task.CompletedTask;
        }

        public Task PlayAsync()
        {
            Operations.Add(nameof(PlayAsync));
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            Operations.Add(nameof(PauseAsync));
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            Operations.Add(nameof(ResumeAsync));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Operations.Add(nameof(StopAsync));
            return Task.CompletedTask;
        }

        public Task ResetAsync()
        {
            Operations.Add(nameof(ResetAsync));
            return Task.CompletedTask;
        }

        public Task SeekToAsync(TimeSpan position)
        {
            SeekTargets.Add(position);
            Operations.Add(nameof(SeekToAsync));
            return Task.CompletedTask;
        }

        public Task SetMutedAsync(bool muted)
        {
            Operations.Add($"{nameof(SetMutedAsync)}:{muted}");
            return Task.CompletedTask;
        }

        public void NotifyTrackTransitionStarting() =>
            Operations.Add(nameof(NotifyTrackTransitionStarting));

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track)
        {
            Operations.Add(nameof(SyncMetadataForTrackAsync));
            return Task.CompletedTask;
        }

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;
        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067

        public void Dispose()
        {
        }
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
                LookUpPath = "/stub"
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
            Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(currentTrackMetadata, "https://stub"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;
    }

    private static TrackMetadata TestMetadata() =>
        new()
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "1",
            LookUpPath = "/test"
        };

    private static List<AudioPlayerTrack> TestPlaylist(int count = 1)
    {
        var meta = TestMetadata();
        var list = new List<AudioPlayerTrack>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new AudioPlayerTrack { PlayItem = new PlayItem(meta, $"https://t{i}") });
        }

        return list;
    }

    private static (PlaybackOperationHandler Handler, StubAudioPlayer Player, RecordingDispatcher Dispatcher,
        ProgressTracker Progress) CreateSut()
    {
        var player = new StubAudioPlayer();
        var dispatcher = new RecordingDispatcher();
        var logger = TestLogging.CreateLogger();
        var playlistService = new StubPlaylistService();
        var progress = new ProgressTracker(playlistService, player, logger);
        var handler = new PlaybackOperationHandler(player, dispatcher, logger, progress);
        return (handler, player, dispatcher, progress);
    }

    [Fact]
    public async Task PlayAsync_returns_when_playlist_null_or_index_invalid()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            await sut.PlayAsync(null, 0, () => throw new InvalidOperationException());
            await sut.PlayAsync(TestPlaylist(), -1, () => throw new InvalidOperationException());
            await sut.PlayAsync(TestPlaylist(), 1, () => throw new InvalidOperationException());

            Assert.Empty(player.Operations);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task PlayAsync_when_paused_and_active_resumes_without_prepare_delegate()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            player.Status = PlayStatus.Paused;
            player.IsActuallyPlayingOrPaused = true;

            await sut.PlayAsync(TestPlaylist(), 0, () => throw new InvalidOperationException());

            Assert.Contains(nameof(StubAudioPlayer.ResumeAsync), player.Operations);
            Assert.DoesNotContain(nameof(StubAudioPlayer.PlayAsync), player.Operations);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task PlayAsync_when_paused_but_not_active_invokes_prepare_delegate()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            player.Status = PlayStatus.Paused;
            player.IsActuallyPlayingOrPaused = false;

            var ran = false;
            await sut.PlayAsync(TestPlaylist(), 0, () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

            Assert.True(ran);
            Assert.DoesNotContain(nameof(StubAudioPlayer.ResumeAsync), player.Operations);
            Assert.DoesNotContain(nameof(StubAudioPlayer.PlayAsync), player.Operations);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task PlayAsync_when_stopped_or_ended_invokes_prepare_delegate()
    {
        foreach (var status in new[] { PlayStatus.Stopped, PlayStatus.Ended })
        {
            var (sut, player, _, progress) = CreateSut();
            try
            {
                player.Status = status;

                var ran = false;
                await sut.PlayAsync(TestPlaylist(), 0, () =>
                {
                    ran = true;
                    return Task.CompletedTask;
                });

                Assert.True(ran);
                Assert.DoesNotContain(nameof(StubAudioPlayer.ResumeAsync), player.Operations);
            }
            finally
            {
                progress.Dispose();
            }
        }
    }

    [Fact]
    public async Task PlayAsync_when_playing_calls_PlayAsync_on_player()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            player.Status = PlayStatus.Playing;

            await sut.PlayAsync(TestPlaylist(), 0, () => throw new InvalidOperationException());

            Assert.Contains(nameof(StubAudioPlayer.PlayAsync), player.Operations);
            Assert.DoesNotContain(nameof(StubAudioPlayer.ResumeAsync), player.Operations);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task PauseAsync_when_not_playing_does_not_pause_or_dispatch()
    {
        var (sut, player, dispatcher, progress) = CreateSut();
        try
        {
            await sut.PauseAsync(9, 2, isPreparingOrPlaying: false);

            Assert.DoesNotContain(nameof(StubAudioPlayer.PauseAsync), player.Operations);
            Assert.Empty(dispatcher.Dispatched);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task PauseAsync_when_playing_pauses_and_dispatches_auto_advancing_false()
    {
        var (sut, player, dispatcher, progress) = CreateSut();
        try
        {
            await sut.PauseAsync(9, 2, isPreparingOrPlaying: true);

            Assert.Contains(nameof(StubAudioPlayer.PauseAsync), player.Operations);
            var action = Assert.Single(dispatcher.Dispatched);
            var auto = Assert.IsType<SetAutoAdvancingAction>(action);
            Assert.False(auto.IsAutoAdvancing);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task SeekForwardAsync_skips_when_not_playing_or_no_position_or_zero_duration()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            await sut.SeekForwardAsync(isPreparingOrPlaying: false);
            Assert.Empty(player.SeekTargets);

            await sut.SeekForwardAsync(isPreparingOrPlaying: true);
            Assert.Empty(player.SeekTargets);

            player.CurrentPosition = TimeSpan.FromSeconds(10);
            player.Duration = TimeSpan.Zero;
            await sut.SeekForwardAsync(isPreparingOrPlaying: true);
            Assert.Empty(player.SeekTargets);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task SeekForwardAsync_seeks_and_clamps_before_duration_minus_buffer()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            player.CurrentPosition = TimeSpan.FromSeconds(30);
            player.Duration = TimeSpan.FromMinutes(1);

            await sut.SeekForwardAsync(isPreparingOrPlaying: true);

            Assert.Single(player.SeekTargets);
            Assert.Equal(TimeSpan.FromSeconds(45), player.SeekTargets[0]);

            player.SeekTargets.Clear();

            player.CurrentPosition = TimeSpan.FromSeconds(59.95);
            player.Duration = TimeSpan.FromMinutes(1);

            await sut.SeekForwardAsync(isPreparingOrPlaying: true);

            var expectedMax = TimeSpan.FromMinutes(1).Subtract(TimeSpan.FromMilliseconds(100));
            Assert.Single(player.SeekTargets);
            Assert.Equal(expectedMax, player.SeekTargets[0]);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task SeekBackwardAsync_skips_when_not_playing_or_no_position()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            await sut.SeekBackwardAsync(isPreparingOrPlaying: false);
            Assert.Empty(player.SeekTargets);

            await sut.SeekBackwardAsync(isPreparingOrPlaying: true);
            Assert.Empty(player.SeekTargets);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task SeekBackwardAsync_clamps_at_zero()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            player.CurrentPosition = TimeSpan.FromSeconds(5);

            await sut.SeekBackwardAsync(isPreparingOrPlaying: true);

            Assert.Single(player.SeekTargets);
            Assert.Equal(TimeSpan.Zero, player.SeekTargets[0]);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task SeekToAsync_skips_when_not_playing()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            await sut.SeekToAsync(TimeSpan.FromSeconds(12), isPreparingOrPlaying: false);
            Assert.Empty(player.SeekTargets);
        }
        finally
        {
            progress.Dispose();
        }
    }

    [Fact]
    public async Task SeekToAsync_clamps_to_duration_and_zero()
    {
        var (sut, player, _, progress) = CreateSut();
        try
        {
            player.Duration = TimeSpan.FromMinutes(1);

            await sut.SeekToAsync(TimeSpan.FromMinutes(2), isPreparingOrPlaying: true);
            Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(player.SeekTargets));

            player.SeekTargets.Clear();

            await sut.SeekToAsync(TimeSpan.FromSeconds(-5), isPreparingOrPlaying: true);
            Assert.Equal(TimeSpan.Zero, Assert.Single(player.SeekTargets));
        }
        finally
        {
            progress.Dispose();
        }
    }
}
