#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackNavigatorNonSectionedHelperTests
{
    private sealed class StubBiblePublicationService : IBiblePublicationService
    {
        public required BiblePublication Publication { get; init; }

        public void Dispose()
        {
        }

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(Publication);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());
    }

    private static BiblePublication ThreeTrackPublication(string code = "vod") =>
        new()
        {
            PublicationCode = code,
            IsVideo = false,
            IsMusic = false,
            Tracks =
            [
                new BiblePublicationTrack { TrackCode = "1", Title = "First" },
                new BiblePublicationTrack { TrackCode = "2", Title = "Second" },
                new BiblePublicationTrack { TrackCode = "3", Title = "Third" },
            ],
        };

    [Fact]
    public async Task GetNextAsync_returns_following_track_in_same_publication()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigatorNonSectionedHelper(bible, TestLogging.CreateLogger());

        var next = await sut.GetNextAsync("E", pub.PublicationCode, "1");

        Assert.Equal(pub.PublicationCode, next.PublicationCode);
        Assert.Equal("2", next.Track.TrackCode);
    }

    [Fact]
    public async Task GetNextAsync_wraps_to_first_when_at_last_track_without_cross_publication_hooks()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigatorNonSectionedHelper(bible, TestLogging.CreateLogger());

        var next = await sut.GetNextAsync("E", pub.PublicationCode, "3");

        Assert.Equal(pub.PublicationCode, next.PublicationCode);
        Assert.Equal("1", next.Track.TrackCode);
    }

    [Fact]
    public async Task GetPreviousAsync_returns_prior_track_in_same_publication()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigatorNonSectionedHelper(bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousAsync("E", pub.PublicationCode, "2");

        Assert.Equal(pub.PublicationCode, prev.PublicationCode);
        Assert.Equal("1", prev.Track.TrackCode);
    }

    [Fact]
    public async Task GetPreviousAsync_wraps_to_last_when_at_first_track()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigatorNonSectionedHelper(bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousAsync("E", pub.PublicationCode, "1");

        Assert.Equal(pub.PublicationCode, prev.PublicationCode);
        Assert.Equal("3", prev.Track.TrackCode);
    }
}
