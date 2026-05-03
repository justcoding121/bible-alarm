#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class PlaybackSessionContextInitializerTests
{
    private sealed class StubPlaylistService : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack =
            new() { TrackCode = "1", Title = "T" };

        private static readonly BiblePublicationSection DummySection =
            new() { SectionCode = "s", Name = "S" };

        public Func<TrackMetadata, Task<PlayItem>>? OnGetPrevious { get; init; }

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(BibleMeta(), "https://stub"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(new PlayItem(BibleMeta(), "https://stub"));

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
            OnGetPrevious?.Invoke(currentTrackMetadata)
            ?? Task.FromResult(new PlayItem(PreMeta(), "https://prev"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata BibleMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "2",
                LookUpPath = "?b",
            };

        private static TrackMetadata PreMeta() =>
            new()
            {
                ScheduleId = 1,
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "40",
                TrackCode = "1",
                LookUpPath = "?pre",
            };
    }

    private static AudioPlayerTrack Track(TrackMetadata meta, string url = "https://x") =>
        new() { PlayItem = new PlayItem(meta, url) };

    [Fact]
    public async Task InitializeAsync_clears_targets_when_playlist_null_or_empty()
    {
        var sut = new PlaybackSessionContextInitializer(new StubPlaylistService());
        PlayItem? sessionMusic = null;
        TrackMetadata? anchor = null;
        TrackMetadata? pre = null;

        await sut.InitializeAsync(null!, pi => sessionMusic = pi, m => anchor = m, m => pre = m);

        Assert.Null(sessionMusic);
        Assert.Null(anchor);
        Assert.Null(pre);

        await sut.InitializeAsync(Array.Empty<AudioPlayerTrack>(), pi => sessionMusic = pi, m => anchor = m, m => pre = m);

        Assert.Null(sessionMusic);
        Assert.Null(anchor);
        Assert.Null(pre);
    }

    [Fact]
    public async Task InitializeAsync_resolves_session_music_anchor_and_pre_anchor()
    {
        var bible = BibleAnchorMeta();
        var musicMeta = new TrackMetadata
        {
            ScheduleId = 1,
            IsBibleContent = false,
            PublicationCode = "song",
            TrackCode = "1",
            LookUpPath = "?m",
        };

        var playlist = new List<AudioPlayerTrack>
        {
            Track(musicMeta),
            Track(bible),
        };

        var sut = new PlaybackSessionContextInitializer(new StubPlaylistService());

        PlayItem? sessionMusic = null;
        TrackMetadata? anchor = null;
        TrackMetadata? pre = null;

        await sut.InitializeAsync(
            playlist,
            pi => sessionMusic = pi,
            m => anchor = m,
            m => pre = m);

        Assert.NotNull(sessionMusic);
        Assert.Same(musicMeta, sessionMusic.Metadata);
        Assert.NotNull(anchor);
        Assert.Equal("2", anchor!.TrackCode);
        Assert.NotNull(pre);
        Assert.Equal("1", pre!.TrackCode);
    }

    [Fact]
    public async Task InitializeAsync_swallows_errors_resolving_pre_anchor()
    {
        var bible = BibleAnchorMeta();
        var playlist = new List<AudioPlayerTrack> { Track(bible) };

        var sut = new PlaybackSessionContextInitializer(new StubPlaylistService
        {
            OnGetPrevious = _ => throw new InvalidOperationException("network"),
        });

        TrackMetadata? pre = null;

        await sut.InitializeAsync(
            playlist,
            _ => { },
            _ => { },
            m => pre = m);

        Assert.Null(pre);
    }

    private static TrackMetadata BibleAnchorMeta() =>
        new()
        {
            ScheduleId = 9,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "2",
            LookUpPath = "?b",
        };
}
