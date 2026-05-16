#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackNavigatorTests
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
            Task.FromResult(new Dictionary<string, Language>());

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

    private sealed class IdleMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
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
    public async Task GetNextBiblePublicationTrack_without_section_delegates_to_flat_navigation()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigator(new IdleMediaService(), bible, TestLogging.CreateLogger());

        var next = await sut.GetNextBiblePublicationTrack("E", pub.PublicationCode, sectionCode: null, trackCode: "1");

        Assert.Equal(pub.PublicationCode, next.PublicationCode);
        Assert.Equal("2", next.Track.TrackCode);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationTrack_without_section_delegates_to_flat_navigation()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigator(new IdleMediaService(), bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousBiblePublicationTrack("E", pub.PublicationCode, sectionCode: null, trackCode: "2");

        Assert.Equal(pub.PublicationCode, prev.PublicationCode);
        Assert.Equal("1", prev.Track.TrackCode);
    }

    [Fact]
    public async Task GetNextBiblePublicationTrack_whitespace_section_delegates_to_flat_navigation()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigator(new IdleMediaService(), bible, TestLogging.CreateLogger());

        var next = await sut.GetNextBiblePublicationTrack("E", pub.PublicationCode, sectionCode: " \t ", trackCode: "1");

        Assert.Equal(pub.PublicationCode, next.PublicationCode);
        Assert.Equal("2", next.Track.TrackCode);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationTrack_whitespace_section_delegates_to_flat_navigation()
    {
        var pub = ThreeTrackPublication();
        var bible = new StubBiblePublicationService { Publication = pub };
        var sut = new TrackNavigator(new IdleMediaService(), bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousBiblePublicationTrack("E", pub.PublicationCode, sectionCode: " \t ", trackCode: "2");

        Assert.Equal(pub.PublicationCode, prev.PublicationCode);
        Assert.Equal("1", prev.Track.TrackCode);
    }

    /// <summary>
    /// Minimal <see cref="IMediaService"/> for sectioned publication navigation (no DB / scope factory).
    /// </summary>
    private sealed class TwoSectionPublicationMediaService : IMediaService
    {
        public required SortedDictionary<string, BiblePublicationSection> Sections { get; init; }
        public required Dictionary<string, SortedDictionary<string, BiblePublicationTrack>> TracksBySectionCode { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode)
        {
            var key = string.IsNullOrWhiteSpace(sectionCode) ? string.Empty : sectionCode;
            if (TracksBySectionCode.TryGetValue(key, out var tracks))
            {
                return Task.FromResult(tracks);
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());
        }

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(Sections);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private static (TwoSectionPublicationMediaService Media, StubBiblePublicationService Bible, string PubCode) CreateTwoSectionPublicationHarness()
    {
        const string pubCode = "nwt";
        var owningPub = new BiblePublication { PublicationCode = pubCode, Id = 1, Name = "New World Translation" };
        var section10 = new BiblePublicationSection
        {
            Id = 1,
            Name = "Section 10",
            SectionCode = "10",
            BiblePublicationId = owningPub.Id,
            BiblePublication = owningPub,
        };
        var section20 = new BiblePublicationSection
        {
            Id = 2,
            Name = "Section 20",
            SectionCode = "20",
            BiblePublicationId = owningPub.Id,
            BiblePublication = owningPub,
        };

        var sections = new SortedDictionary<string, BiblePublicationSection>(Comparer<string>.Create((a, b) =>
            SectionCodeHelper.SectionCodeComparer.Compare(a, b)))
        {
            ["10"] = section10,
            ["20"] = section20,
        };

        var tracks10 = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer)
        {
            ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "Ten-A" },
            ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "Ten-B" },
        };
        var tracks20 = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer)
        {
            ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "Twenty-A" },
        };

        var media = new TwoSectionPublicationMediaService
        {
            Sections = sections,
            TracksBySectionCode = new Dictionary<string, SortedDictionary<string, BiblePublicationTrack>>(StringComparer.OrdinalIgnoreCase)
            {
                ["10"] = tracks10,
                ["20"] = tracks20,
            },
        };

        var bible = new StubBiblePublicationService { Publication = new BiblePublication { PublicationCode = pubCode, Tracks = [] } };
        return (media, bible, pubCode);
    }

    [Fact]
    public async Task GetNextBiblePublicationTrack_sectioned_returns_next_track_in_same_section()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        var next = await sut.GetNextBiblePublicationTrack("E", pubCode, sectionCode: "10", trackCode: "1");

        Assert.Equal(pubCode, next.PublicationCode);
        Assert.Equal("10", next.Section?.SectionCode);
        Assert.Equal("2", next.Track.TrackCode);
    }

    [Fact]
    public async Task GetNextBiblePublicationTrack_sectioned_advances_to_first_track_of_next_section_at_section_end()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        var next = await sut.GetNextBiblePublicationTrack("E", pubCode, sectionCode: "10", trackCode: "2");

        Assert.Equal(pubCode, next.PublicationCode);
        Assert.Equal("20", next.Section?.SectionCode);
        Assert.Equal("1", next.Track.TrackCode);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationTrack_sectioned_returns_previous_track_in_same_section()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousBiblePublicationTrack("E", pubCode, sectionCode: "10", trackCode: "2");

        Assert.Equal(pubCode, prev.PublicationCode);
        Assert.Equal("10", prev.Section?.SectionCode);
        Assert.Equal("1", prev.Track.TrackCode);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationTrack_sectioned_wraps_to_last_track_of_previous_section()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousBiblePublicationTrack("E", pubCode, sectionCode: "20", trackCode: "1");

        Assert.Equal(pubCode, prev.PublicationCode);
        Assert.Equal("10", prev.Section?.SectionCode);
        Assert.Equal("2", prev.Track.TrackCode);
    }

    [Fact]
    public async Task GetNextBiblePublicationSection_forward_wraps_from_last_to_first()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        var next = await sut.GetNextBiblePublicationSection("E", pubCode, sectionCode: "20");

        Assert.Equal("10", next.Key);
        Assert.Equal("10", next.Value.SectionCode);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationSection_backward_wraps_from_first_to_last()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        var prev = await sut.GetPreviousBiblePublicationSection("E", pubCode, sectionCode: "10");

        Assert.Equal("20", prev.Key);
        Assert.Equal("20", prev.Value.SectionCode);
    }

    [Fact]
    public async Task GetNextBiblePublicationTrack_sectioned_throws_when_section_not_in_catalog()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var sut = new TrackNavigator(media, bible, TestLogging.CreateLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetNextBiblePublicationTrack("E", pubCode, sectionCode: "99", trackCode: "1"));
    }

    [Fact]
    public async Task GetNextBiblePublicationSection_reuses_sections_cache_within_ttl()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var countingMedia = new CountingSectionMediaService(media);
        var sut = new TrackNavigator(countingMedia, bible, TestLogging.CreateLogger());

        _ = await sut.GetNextBiblePublicationSection("E", pubCode, sectionCode: "10");
        countingMedia.SectionsFetchCount = 0;

        _ = await sut.GetNextBiblePublicationSection("E", pubCode, sectionCode: "20");

        Assert.Equal(0, countingMedia.SectionsFetchCount);
    }

    [Fact]
    public async Task GetPreviousBiblePublicationTrack_sectioned_reuses_tracks_cache_within_same_section()
    {
        var (media, bible, pubCode) = CreateTwoSectionPublicationHarness();
        var countingMedia = new CountingSectionMediaService(media);
        var sut = new TrackNavigator(countingMedia, bible, TestLogging.CreateLogger());

        _ = await sut.GetNextBiblePublicationTrack("E", pubCode, sectionCode: "10", trackCode: "1");
        countingMedia.TracksFetchCount = 0;

        _ = await sut.GetPreviousBiblePublicationTrack("E", pubCode, sectionCode: "10", trackCode: "2");

        Assert.Equal(0, countingMedia.TracksFetchCount);
    }

    private sealed class CountingSectionMediaService(TwoSectionPublicationMediaService inner) : IMediaService
    {
        public int SectionsFetchCount { get; set; }

        public int TracksFetchCount { get; set; }

        public void Dispose() => inner.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode)
        {
            TracksFetchCount++;
            return inner.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);
        }

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null)
        {
            SectionsFetchCount++;
            return inner.GetBiblePublicationSections(languageCode, versionCode, progress);
        }

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            inner.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            inner.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            inner.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => inner.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) => inner.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            inner.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => inner.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            inner.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            inner.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            inner.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            inner.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            inner.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) => inner.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            inner.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            inner.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            inner.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            inner.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            inner.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }
}
