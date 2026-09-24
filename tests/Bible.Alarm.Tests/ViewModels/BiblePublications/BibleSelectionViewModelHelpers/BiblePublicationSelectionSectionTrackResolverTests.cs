#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionSectionTrackResolverTests
{
    private sealed class StubMedia : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

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

    [Fact]
    public async Task GetFirstTrackForNonSectionedAsync_WhenBiblePublicationServiceNull_ReturnsEmptyTuple()
    {
        var sut = new BiblePublicationSelectionSectionTrackResolver(
            new StubMedia(),
            new MediaTestScopeFactory(
                new DbContextOptionsBuilder<MediaDbContext>()
                    .UseSqlite("Data Source=:memory:")
                    .Options),
            biblePublicationService: null,
            languageContentService: null);

        var result = await sut.GetFirstTrackForNonSectionedAsync("E", "nwt");

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_WhenPublicationMissing_ReturnsFalse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var sut = new BiblePublicationSelectionSectionTrackResolver(
            new StubMedia(),
            new MediaTestScopeFactory(options),
            biblePublicationService: null,
            languageContentService: null);

        Assert.False(await sut.CheckIfPublicationWithFirstSectionCatalogedAsync("nwt", "E"));
    }

    [Fact]
    public async Task GetFirstSectionAndTrackFromSectionsAsync_WhenNoLanguagePublicationHasEmptySectionTracks_ReturnsEmptyTuple()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var publication = new BiblePublication
            {
                Name = "NWT",
                PublicationCode = "nwt",
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            publication.Sections.Add(new BiblePublicationSection
            {
                Name = "Matthew",
                SectionCode = "40",
                BiblePublication = publication,
            });
            seed.BiblePublications.Add(publication);
            await seed.SaveChangesAsync();
        }

        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["40"] = new BiblePublicationSection { SectionCode = "40", Name = "Matthew" },
        };

        var sut = new BiblePublicationSelectionSectionTrackResolver(
            new StubMedia(),
            new MediaTestScopeFactory(options),
            biblePublicationService: null,
            languageContentService: null);

        var result = await sut.GetFirstSectionAndTrackFromSectionsAsync(languageCode: null, "nwt", sections);

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackFromSectionsAsync_returns_first_track_for_no_language_section_in_db()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var publication = new BiblePublication
            {
                Name = "Kingdom Melodies",
                PublicationCode = "iam",
                LanguageId = null,
                IsVideo = false,
                IsMusic = true,
            };
            var section = new BiblePublicationSection
            {
                Name = "Disc 1",
                SectionCode = "iam-1",
                BiblePublication = publication,
            };
            publication.Sections.Add(section);
            section.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "190",
                Title = "Melody 190",
                Section = section,
                Publication = publication,
            });
            seed.BiblePublications.Add(publication);
            await seed.SaveChangesAsync();
        }

        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["iam-1"] = new BiblePublicationSection { SectionCode = "iam-1", Name = "Disc 1" },
        };

        var sut = new BiblePublicationSelectionSectionTrackResolver(
            new StubMedia(),
            new MediaTestScopeFactory(options),
            biblePublicationService: null,
            languageContentService: null);

        var result = await sut.GetFirstSectionAndTrackFromSectionsAsync(languageCode: null, "iam", sections);

        Assert.Equal("iam-1", result.SectionCode);
        Assert.Equal("190", result.TrackCode);
        Assert.Equal("Disc 1", result.SectionName);
        Assert.Equal("Melody 190", result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstTrackForNonSectionedAsync_returns_track_when_service_and_db_have_flat_tracks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var publication = new BiblePublication
            {
                Name = "Drama",
                PublicationCode = "sjjc",
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            publication.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "3",
                Title = "Scene 3",
                Publication = publication,
            });
            seed.BiblePublications.Add(publication);
            await seed.SaveChangesAsync();
        }

        var sut = new BiblePublicationSelectionSectionTrackResolver(
            new StubMedia(),
            new MediaTestScopeFactory(options),
            biblePublicationService: new FlatTrackBiblePublicationService(),
            languageContentService: null);

        var result = await sut.GetFirstTrackForNonSectionedAsync(languageCode: null, "sjjc");

        Assert.Null(result.SectionCode);
        Assert.Equal("3", result.TrackCode);
        Assert.Equal("Scene 3", result.TrackTitle);
    }

    private sealed class FlatTrackBiblePublicationService : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());
    }
}
