#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class PlaybackIndefiniteResolverTests
{
    private sealed class RecordingPlaylistService : IPlaylistService
    {
        private static readonly BiblePublicationTrack DummyTrack = new() { TrackCode = "1", Title = "T" };
        private static readonly BiblePublicationSection DummySection = new() { SectionCode = "s", Name = "S" };

        public List<TrackMetadata> NextCalls { get; } = [];
        public List<TrackMetadata> PreviousCalls { get; } = [];

        public PlayItem NextPlayItemResponse { get; init; } =
            new(Bible("E", "nwt", "40", "99"), "https://next");

        public PlayItem PreviousPlayItemResponse { get; init; } =
            new(Bible("E", "nwt", "40", "88"), "https://prev");

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(Bible("E", "nwt", "40", "1"), "https://stub"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(new PlayItem(Bible("E", "nwt", "40", "1"), "https://stub"));

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
            IFetchProgress? sectionFetchProgress = null)
        {
            NextCalls.Add(currentTrackMetadata);
            return Task.FromResult(NextPlayItemResponse);
        }

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null)
        {
            PreviousCalls.Add(currentTrackMetadata);
            return Task.FromResult(PreviousPlayItemResponse);
        }

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata Bible(string lang, string pub, string section, string track) =>
            new()
            {
                IsBibleContent = true,
                LanguageCode = lang,
                PublicationCode = pub,
                SectionCode = section,
                TrackCode = track,
                LookUpPath = "/b",
            };
    }

    private static TrackMetadata Bible(string lang, string pub, string? section, string track) =>
        new()
        {
            IsBibleContent = true,
            LanguageCode = lang,
            PublicationCode = pub,
            SectionCode = section,
            TrackCode = track,
            LookUpPath = "/b",
        };

    private static TrackMetadata Music(string pub = "song", string track = "1") =>
        new()
        {
            IsBibleContent = false,
            PublicationCode = pub,
            TrackCode = track,
            LookUpPath = "https://music.example/x",
        };

    private static PlayItem SessionMusic() =>
        new(Music("song", "m1"), "https://session");

    [Fact]
    public void IsSameBibleTrack_matches_when_codes_align()
    {
        var a = Bible("e", "nwt", "40", "01");
        var b = Bible("E", "NWT", "40", "1");

        Assert.True(PlaybackIndefiniteResolver.IsSameBibleTrack(a, b));
    }

    [Fact]
    public void IsSameBibleTrack_false_when_play_types_or_codes_differ()
    {
        var bible = Bible("E", "nwt", "40", "1");
        var music = Music();

        Assert.False(PlaybackIndefiniteResolver.IsSameBibleTrack(bible, music));

        var otherSection = Bible("E", "nwt", "41", "1");
        Assert.False(PlaybackIndefiniteResolver.IsSameBibleTrack(bible, otherSection));
    }

    [Fact]
    public async Task ResolveNextPlayItemAsync_without_injection_delegates_to_playlist_with_current()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);
        var current = Bible("E", "jwpub", null, "5");

        var result = await sut.ResolveNextPlayItemAsync(current, null, null, null, null);

        Assert.Same(playlists.NextPlayItemResponse, result);
        Assert.Single(playlists.NextCalls);
        Assert.Same(current, playlists.NextCalls[0]);
        Assert.Empty(playlists.PreviousCalls);
    }

    [Fact]
    public async Task ResolveNextPlayItemAsync_returns_session_music_when_current_matches_pre_anchor()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);

        var pre = Bible("E", "nwt", "40", "2");
        var anchor = Bible("E", "nwt", "40", "5");
        var session = SessionMusic();

        var result = await sut.ResolveNextPlayItemAsync(pre, session, anchor, pre, null);

        Assert.Same(session, result);
        Assert.Empty(playlists.NextCalls);
    }

    [Fact]
    public async Task ResolveNextPlayItemAsync_after_music_asks_playlist_for_pre_anchor()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);

        var pre = Bible("E", "nwt", "40", "2");
        var anchor = Bible("E", "nwt", "40", "5");
        var session = SessionMusic();
        var currentMusic = Music();

        var result = await sut.ResolveNextPlayItemAsync(currentMusic, session, anchor, pre, null);

        Assert.Same(playlists.NextPlayItemResponse, result);
        Assert.Single(playlists.NextCalls);
        Assert.True(PlaybackIndefiniteResolver.IsSameBibleTrack(pre, playlists.NextCalls[0]));
    }

    [Fact]
    public async Task ResolvePreviousPlayItemAsync_returns_session_music_when_current_matches_anchor()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);

        var pre = Bible("E", "nwt", "40", "2");
        var anchor = Bible("E", "nwt", "40", "5");
        var session = SessionMusic();

        var result = await sut.ResolvePreviousPlayItemAsync(anchor, session, anchor, pre, null);

        Assert.Same(session, result);
        Assert.Empty(playlists.PreviousCalls);
    }

    [Fact]
    public async Task ResolvePreviousPlayItemAsync_after_music_asks_playlist_for_anchor()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);

        var pre = Bible("E", "nwt", "40", "2");
        var anchor = Bible("E", "nwt", "40", "5");
        var session = SessionMusic();
        var currentMusic = Music();

        var result = await sut.ResolvePreviousPlayItemAsync(currentMusic, session, anchor, pre, null);

        Assert.Same(playlists.PreviousPlayItemResponse, result);
        Assert.Single(playlists.PreviousCalls);
        Assert.True(PlaybackIndefiniteResolver.IsSameBibleTrack(anchor, playlists.PreviousCalls[0]));
    }

    [Fact]
    public async Task ResolveNextPlayItemAsync_with_injection_bible_other_than_pre_anchor_delegates_to_playlist()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);

        var pre = Bible("E", "nwt", "40", "2");
        var anchor = Bible("E", "nwt", "40", "5");
        var session = SessionMusic();
        var currentBible = Bible("E", "nwt", "40", "3");

        var result = await sut.ResolveNextPlayItemAsync(currentBible, session, anchor, pre, null);

        Assert.Same(playlists.NextPlayItemResponse, result);
        Assert.Single(playlists.NextCalls);
        Assert.Same(currentBible, playlists.NextCalls[0]);
    }

    [Fact]
    public async Task ResolvePreviousPlayItemAsync_with_injection_bible_other_than_anchor_delegates_to_playlist()
    {
        var playlists = new RecordingPlaylistService();
        var sut = new PlaybackIndefiniteResolver(playlists);

        var pre = Bible("E", "nwt", "40", "2");
        var anchor = Bible("E", "nwt", "40", "5");
        var session = SessionMusic();
        var currentBible = Bible("E", "nwt", "40", "3");

        var result = await sut.ResolvePreviousPlayItemAsync(currentBible, session, anchor, pre, null);

        Assert.Same(playlists.PreviousPlayItemResponse, result);
        Assert.Single(playlists.PreviousCalls);
        Assert.Same(currentBible, playlists.PreviousCalls[0]);
    }
}
