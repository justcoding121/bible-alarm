#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigatorHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class TrackNavigatorSectionCatalogerTests
{
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

    private static TrackNavigatorSectionCataloger SutWithoutDb(Func<string, string, Task<SortedDictionary<string, BiblePublicationSection>>> getSections) =>
        new(
            new IdleMediaService(),
            languageContentService: null,
            scopeFactory: null,
            TestLogging.CreateLogger(),
            getSections);

    [Fact]
    public async Task GetDiscoveredSectionCodesAsync_without_scope_factory_returns_delegate_keys()
    {
        var sut = SutWithoutDb(async (_, _) =>
        {
            await Task.CompletedTask;
            return new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
            {
                ["sec-a"] = new BiblePublicationSection { SectionCode = "sec-a", Name = "Section A", BiblePublicationId = 1 },
            };
        });

        var codes = await sut.GetDiscoveredSectionCodesAsync("E", "nwt");

        Assert.Single(codes);
        Assert.Contains("sec-a", codes);
    }

    [Fact]
    public async Task IsPublicationWithoutLanguageAsync_without_scope_factory_returns_false()
    {
        var sut = SutWithoutDb((_, _) => Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)));

        Assert.False(await sut.IsPublicationWithoutLanguageAsync(AppConstants.Media.MelodyMusicPublicationCodeIam));
    }

    [Fact]
    public async Task EnsureSectionCatalogedAsync_returns_false_when_section_not_discovered()
    {
        var sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["gen"] = new BiblePublicationSection { SectionCode = "gen", Name = "Genesis", BiblePublicationId = 1 },
        };

        var sut = new TrackNavigatorSectionCataloger(
            new IdleMediaService(),
            languageContentService: new StubLanguageContentService(),
            scopeFactory: new StubScopeFactory(),
            TestLogging.CreateLogger(),
            (_, _) => Task.FromResult(sections));

        var ok = await sut.EnsureSectionCatalogedAsync(
            "E",
            "nwt",
            "exodus",
            sectionFetchProgress: null,
            clearSectionsCache: () => { },
            clearTracksCache: () => { });

        Assert.False(ok);
    }

    [Fact]
    public async Task EnsureSectionCatalogedAsync_returns_true_when_cached_section_already_has_tracks()
    {
        var tracks = new SortedDictionary<string, BiblePublicationTrack>
        {
            ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "Track 1" },
        };

        var media = new IdleMediaService();
        var sut = new TrackNavigatorSectionCataloger(
            new MediaServiceWithTracks(media, tracks),
            languageContentService: new StubLanguageContentService(),
            scopeFactory: new StubScopeFactory(),
            TestLogging.CreateLogger(),
            (_, _) => Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
            {
                ["gen"] = new BiblePublicationSection { SectionCode = "gen", Name = "Genesis", BiblePublicationId = 1 },
            }));

        var ok = await sut.EnsureSectionCatalogedAsync(
            "E",
            "nwt",
            "gen",
            sectionFetchProgress: null,
            clearSectionsCache: () => { },
            clearTracksCache: () => { });

        Assert.True(ok);
    }

    [Fact]
    public async Task GetDiscoveredSectionCodesAsync_queries_db_when_scope_factory_available()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var sut = new TrackNavigatorSectionCataloger(
                new IdleMediaService(),
                languageContentService: null,
                scopeFactory: new SqliteScopeFactory(options),
                TestLogging.CreateLogger(),
                (_, _) => Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fallback"] = new BiblePublicationSection { SectionCode = "fallback", Name = "Fallback", BiblePublicationId = 1 },
                }));

            var codes = await sut.GetDiscoveredSectionCodesAsync("E", "nwt");

            Assert.Empty(codes);
        }
    }

    private sealed class StubLanguageContentService : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode, string languageCode, bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new StubScope();

        private sealed class StubScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();

            public void Dispose()
            {
            }
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }

    private sealed class SqliteScopeFactory(DbContextOptions<MediaDbContext> options) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new SqliteScope(options);

        private sealed class SqliteScope(DbContextOptions<MediaDbContext> options) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new SqliteServiceProvider(options);

            public void Dispose()
            {
            }
        }

        private sealed class SqliteServiceProvider(DbContextOptions<MediaDbContext> options) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(MediaDbContext) ? new MediaDbContext(options) : null;
        }
    }

    private sealed class MediaServiceWithTracks(IdleMediaService inner, SortedDictionary<string, BiblePublicationTrack> tracks) : IMediaService
    {
        public void Dispose() => inner.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(tracks);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            inner.GetBiblePublicationSections(languageCode, versionCode, progress);

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

    private static async Task<(Microsoft.Data.Sqlite.SqliteConnection Connection, DbContextOptions<MediaDbContext> Options)> CreateConnectionAndOptionsAsync()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        return (connection, options);
    }

    [Fact]
    public async Task EnsureSectionCatalogedAsync_returns_false_when_dependencies_missing()
    {
        var sut = SutWithoutDb((_, _) => Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)));

        var ok = await sut.EnsureSectionCatalogedAsync(
            "E",
            "nwt",
            "gen",
            sectionFetchProgress: null,
            clearSectionsCache: () => { },
            clearTracksCache: () => { });

        Assert.False(ok);
    }
}
