#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceTests
{
    static MediaServiceTests()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
    }
    private sealed class IdleMediaIndexService : IMediaIndexService
    {
        public string IndexRoot => string.Empty;
        public bool WasIndexReplacedThisRun => false;

        public void Dispose()
        {
        }

        public Task MigrateNonEnglishDataIfNeededAsync() => Task.CompletedTask;

        public Task Verify() => Task.CompletedTask;
    }

    private sealed class EmptyDistinctLanguagesBiblePublicationService : IBiblePublicationService
    {
        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(
            string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnusedBiblePublicationSectionService : IBiblePublicationSectionService
    {
        public Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, string sectionCode,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<string, BiblePublicationSection>>
            GetSectionsByPublicationWithoutLanguageAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<BiblePublicationSection?> GetSectionAsync(
            string languageCode, string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedBiblePublicationTrackService : IBiblePublicationTrackService
    {
        public Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksBySectionAsync(
            string languageCode, string publicationCode, string? sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, string? sectionCode,
            string trackCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string? sectionCode, string trackCode,
            string url, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedMelodyMusicService : IMelodyMusicService
    {
        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(
            string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(
            string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedVocalMusicService : IVocalMusicService
    {
        public Task<VocalMusic?> GetByLanguageAndCodeAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(
            string languageCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedLanguageContentService : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(
            string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchPublicationTracksAsync(
            string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchPublicationSectionsAsync(
            string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchSectionTracksAsync(
            string publicationCode, string sectionCode, string languageCode,
            bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> EnsurePublicationExistsAsync(
            string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchFirstPublicationForLanguageAsync(
            string languageCode, string? categoryName = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> EnsureAllPublicationsForLanguageAsync(
            string languageCode, string? categoryName = null, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> EnsureAllSectionsForPublicationAsync(
            string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchFirstSectionOnlyAsync(
            string publicationCode, string firstSectionCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
    }

    /// <summary>
    /// Empty in-memory Media DB scope. GetBiblePublications / GetVocalMusicReleases / track lookups
    /// open scopes for placeholder and no-language queries even when stub services supply the main data.
    /// </summary>
    private sealed class EmptyMediaDbScopeFactory : IServiceScopeFactory, IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly MediaTestScopeFactory factory;

        public EmptyMediaDbScopeFactory()
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            using (var init = new MediaDbContext(options))
            {
                init.Database.EnsureCreated();
            }

            factory = new MediaTestScopeFactory(
                new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options);
        }

        public IServiceScope CreateScope() => factory.CreateScope();

        public void Dispose() => connection.Dispose();
    }

    private sealed class CountingMediaIndexService : IMediaIndexService
    {
        public int VerifyCount { get; private set; }
        public string IndexRoot => string.Empty;
        public bool WasIndexReplacedThisRun => false;

        public void Dispose()
        {
        }

        public Task MigrateNonEnglishDataIfNeededAsync() => Task.CompletedTask;

        public Task Verify()
        {
            VerifyCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubBiblePublicationSectionService : IBiblePublicationSectionService
    {
        public SortedDictionary<string, BiblePublicationSection>? WithoutLanguageResult { get; set; }
        public SortedDictionary<string, BiblePublicationSection>? ByPublicationResult { get; set; }
        public int GetSectionsByPublicationCallCount { get; private set; }

        public void Dispose()
        {
        }

        public Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, string sectionCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default)
        {
            GetSectionsByPublicationCallCount++;
            return Task.FromResult(ByPublicationResult ?? new SortedDictionary<string, BiblePublicationSection>());
        }

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationWithoutLanguageAsync(
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(WithoutLanguageResult ?? new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetSectionAsync(string languageCode, string publicationCode,
            string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSection?>(new BiblePublicationSection { SectionCode = sectionCode });
    }

    private sealed class RecordingBiblePublicationTrackService : IBiblePublicationTrackService
    {
        public (string Lang, string Pub, string? Section, string Track, string Url)? LastUpdate { get; private set; }

        public void Dispose()
        {
        }

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksBySectionAsync(string languageCode,
            string publicationCode, string? sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode,
            string? sectionCode, string trackCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationTrack?>(new BiblePublicationTrack { TrackCode = trackCode });

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string? sectionCode,
            string trackCode, string url, CancellationToken cancellationToken = default)
        {
            LastUpdate = (languageCode, publicationCode, sectionCode, trackCode, url);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMelodyMusicService : IMelodyMusicService
    {
        public (string Pub, string Track, string Url)? LastUpdate { get; private set; }
        public Dictionary<string, MelodyMusic> AllResult { get; set; } = [];

        public void Dispose()
        {
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MelodyMusic?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AllResult);

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode,
            string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default)
        {
            LastUpdate = (publicationCode, trackCode, url);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingVocalMusicService : IVocalMusicService
    {
        public (string Lang, string Pub, string Track, string Url)? LastUpdate { get; private set; }

        public void Dispose()
        {
        }

        public Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<VocalMusic?>(null);

        public Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default)
        {
            LastUpdate = (languageCode, publicationCode, trackCode, url);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingPublicationsBiblePublicationService : IBiblePublicationService
    {
        public int GetByLanguageCodeCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode,
            string? categoryName = null, bool filterIsMusicForMusicCategory = false,
            CancellationToken cancellationToken = default)
        {
            GetByLanguageCodeCalls++;
            return Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
            {
                ["nwt"] = new BiblePublication { Id = 1, PublicationCode = "nwt", Name = "New World Translation" },
            });
        }

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private sealed class DistinctLanguagesBiblePublicationService : IBiblePublicationService
    {
        private readonly Dictionary<string, Language> languages;

        public DistinctLanguagesBiblePublicationService(Dictionary<string, Language> languages) =>
            this.languages = languages;

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(languages);

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode,
            string? categoryName = null, bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private static MediaService CreateSut(
        IMediaIndexService? index = null,
        IBiblePublicationService? biblePubs = null,
        IBiblePublicationSectionService? sections = null,
        IBiblePublicationTrackService? tracks = null,
        IMelodyMusicService? melody = null,
        IVocalMusicService? vocal = null,
        ILanguageContentService? languageContent = null,
        IServiceScopeFactory? scopeFactory = null) =>
        new(new MediaServiceDependencies(
            index ?? new IdleMediaIndexService(),
            biblePubs ?? new EmptyDistinctLanguagesBiblePublicationService(),
            sections ?? new UnusedBiblePublicationSectionService(),
            tracks ?? new UnusedBiblePublicationTrackService(),
            melody ?? new UnusedMelodyMusicService(),
            vocal ?? new UnusedVocalMusicService(),
            languageContent ?? new UnusedLanguageContentService(),
            scopeFactory ?? new EmptyMediaDbScopeFactory()));

    private static MediaService CreateSutForVocalLanguages(EmptyDistinctLanguagesBiblePublicationService biblePubs) =>
        CreateSut(biblePubs: biblePubs);

    private sealed class NoLanguagePublicationDbScope : IDisposable
    {
        private readonly SqliteConnection connection;

        public MediaTestScopeFactory Factory { get; }

        public NoLanguagePublicationDbScope(string publicationCode)
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            using (var init = new MediaDbContext(options))
            {
                init.Database.EnsureCreated();
                init.BiblePublications.Add(new BiblePublication
                {
                    Name = "No Lang",
                    PublicationCode = publicationCode,
                    LanguageId = null,
                    IsVideo = false,
                    IsMusic = true,
                });
                init.SaveChanges();
            }

            Factory = new MediaTestScopeFactory(
                new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options);
        }

        public void Dispose() => connection.Dispose();
    }

    [Fact]
    public async Task IsPublicationWithoutLanguageAsync_returns_false_when_code_is_whitespace()
    {
        var sut = CreateSutForVocalLanguages(new EmptyDistinctLanguagesBiblePublicationService());

        var result = await sut.IsPublicationWithoutLanguageAsync("   ");

        Assert.False(result);
    }

    [Fact]
    public async Task GetVocalMusicLanguages_adds_default_english_when_distinct_list_is_empty()
    {
        var sut = CreateSutForVocalLanguages(new EmptyDistinctLanguagesBiblePublicationService());

        var langs = await sut.GetVocalMusicLanguages();

        Assert.True(langs.ContainsKey(AppConstants.Media.DefaultLanguageCode));
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, langs[AppConstants.Media.DefaultLanguageCode].LanguageCode);
    }

    [Fact]
    public async Task GetVocalMusicLanguages_returns_distinct_languages_when_catalog_has_entries()
    {
        var langs = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            ["S"] = new Language { LanguageCode = "S" },
        };
        var sut = CreateSut(biblePubs: new DistinctLanguagesBiblePublicationService(langs));

        var result = await sut.GetVocalMusicLanguages();

        Assert.Single(result);
        Assert.Equal("S", result["S"].LanguageCode);
    }

    [Fact]
    public async Task IsPublicationWithoutLanguageAsync_reads_from_db_and_caches()
    {
        const string pub = "iam-test";
        using var db = new NoLanguagePublicationDbScope(pub);
        var sut = CreateSut(scopeFactory: db.Factory);

        var first = await sut.IsPublicationWithoutLanguageAsync(pub);
        var second = await sut.IsPublicationWithoutLanguageAsync(pub);

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public async Task IsPublicationWithoutLanguageAsync_returns_false_when_publication_is_language_bound()
    {
        using var db = new NoLanguagePublicationDbScope("iam-test");
        var sut = CreateSut(scopeFactory: db.Factory);
        var result = await sut.IsPublicationWithoutLanguageAsync("nwt");
        Assert.False(result);
    }

    [Fact]
    public async Task GetBiblePublicationSections_routes_to_without_language_when_publication_has_no_language()
    {
        const string pub = "iam-sec";
        var sections = new StubBiblePublicationSectionService
        {
            WithoutLanguageResult = new SortedDictionary<string, BiblePublicationSection>
            {
                ["iam-1"] = new() { SectionCode = "iam-1", Name = "Disc 1" },
            },
        };
        using var db = new NoLanguagePublicationDbScope(pub);
        var finalSut = CreateSut(sections: sections, scopeFactory: db.Factory);

        var result = await finalSut.GetBiblePublicationSections("E", pub);

        Assert.Single(result);
        Assert.Equal("iam-1", result.First().Key);
        Assert.Equal(0, sections.GetSectionsByPublicationCallCount);
        finalSut.Dispose();
    }

    [Fact]
    public async Task GetSectionsForPublicationWithoutLanguage_returns_empty_sorted_dictionary_when_none()
    {
        var sections = new StubBiblePublicationSectionService();
        var sut = CreateSut(sections: sections);

        var result = await sut.GetSectionsForPublicationWithoutLanguage("missing");

        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateTrackUrlAsync_routes_bible_melody_and_vocal_by_metadata()
    {
        var bibleTracks = new RecordingBiblePublicationTrackService();
        var melody = new RecordingMelodyMusicService();
        var vocal = new RecordingVocalMusicService();
        var sut = CreateSut(tracks: bibleTracks, melody: melody, vocal: vocal);

        await sut.UpdateTrackUrlAsync(new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "5",
        }, "https://cdn.example/bible.mp3");

        await sut.UpdateTrackUrlAsync(new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = null!,
            PublicationCode = "iam",
            TrackCode = "190",
        }, "https://cdn.example/melody.mp3");

        await sut.UpdateTrackUrlAsync(new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "sjjc",
            TrackCode = "3",
        }, "https://cdn.example/vocal.mp3");

        Assert.Equal(("E", "nwt", "1", "5", "https://cdn.example/bible.mp3"), bibleTracks.LastUpdate);
        Assert.Equal(("iam", "190", "https://cdn.example/melody.mp3"), melody.LastUpdate);
        Assert.Equal(("E", "sjjc", "3", "https://cdn.example/vocal.mp3"), vocal.LastUpdate);
    }

    [Fact]
    public async Task GetMelodyMusicReleases_verifies_index_and_returns_catalog()
    {
        var index = new CountingMediaIndexService();
        var melody = new RecordingMelodyMusicService
        {
            AllResult = new Dictionary<string, MelodyMusic>
            {
                ["iam"] = new MelodyMusic
                {
                    Publication = new BiblePublication { PublicationCode = "iam", Name = "Kingdom Melodies" },
                },
            },
        };
        var sut = CreateSut(index: index, melody: melody);

        var result = await sut.GetMelodyMusicReleases();

        Assert.Equal(1, index.VerifyCount);
        Assert.True(result.ContainsKey("iam"));
    }

    [Fact]
    public async Task GetBiblePublicationLanguages_delegates_to_bible_publication_service()
    {
        var langs = new Dictionary<string, Language> { ["E"] = new() { LanguageCode = "E" } };
        var bible = new DistinctLanguagesBiblePublicationService(langs);
        var index = new CountingMediaIndexService();
        var sut = CreateSut(index: index, biblePubs: bible);

        var result = await sut.GetBiblePublicationLanguages("Bible");

        Assert.Equal(1, index.VerifyCount);
        Assert.Equal("E", result["E"].LanguageCode);
    }

    [Fact]
    public void InvalidateBiblePublicationsCache_does_not_throw_for_music_category()
    {
        var sut = CreateSut();
        var ex = Record.Exception(() => sut.InvalidateBiblePublicationsCache("E", AppConstants.Media.BiblePublicationCategoryMusic));
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = CreateSut();
        sut.Dispose();
        var ex = Record.Exception(() => sut.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task GetBiblePublicationTrack_and_section_delegates_to_services()
    {
        var sections = new StubBiblePublicationSectionService();
        var tracks = new RecordingBiblePublicationTrackService();
        var sut = CreateSut(sections: sections, tracks: tracks);

        var section = await sut.GetBiblePublicationSection("E", "nwt", "1");
        var track = await sut.GetBiblePublicationTrack("E", "nwt", "1", "5");

        Assert.NotNull(section);
        Assert.Equal("1", section!.SectionCode);
        Assert.NotNull(track);
        Assert.Equal("5", track!.TrackCode);
    }

    [Fact]
    public async Task GetBiblePublications_uses_in_memory_cache_for_repeat_read_only_requests()
    {
        var bible = new CountingPublicationsBiblePublicationService();
        var index = new CountingMediaIndexService();
        var sut = CreateSut(index: index, biblePubs: bible);

        var first = await sut.GetBiblePublications("E", "Bible");
        var second = await sut.GetBiblePublications("E", "Bible");

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, bible.GetByLanguageCodeCalls);
        // Verify runs inside the cached load path, so a cache hit does not re-verify.
        Assert.Equal(1, index.VerifyCount);
    }

    [Fact]
    public async Task GetMelodyMusicTracks_delegates_to_melody_service()
    {
        var melody = new ConfigurableMelodyMusicService
        {
            TracksByCode = new SortedDictionary<int, MusicTrack>
            {
                [1] = new MusicTrack { TrackCode = "1", Title = "M1", Url = "", LookUpPath = "" },
            },
        };
        var index = new CountingMediaIndexService();
        var sut = CreateSut(index: index, melody: melody);

        var result = await sut.GetMelodyMusicTracks("iam");

        Assert.Equal(1, index.VerifyCount);
        Assert.Single(result);
        Assert.Equal("1", result[1].TrackCode);
    }

    [Fact]
    public async Task GetMelodyMusicTracksBySection_delegates_to_melody_service()
    {
        var melody = new ConfigurableMelodyMusicService
        {
            TracksBySection = new SortedDictionary<int, MusicTrack>
            {
                [105] = new MusicTrack { TrackCode = "105", Title = "M105", Url = "", LookUpPath = "" },
            },
        };
        var sut = CreateSut(melody: melody);

        var result = await sut.GetMelodyMusicTracksBySection("iam", "iam-1");

        Assert.Single(result);
        Assert.Equal("105", result[105].TrackCode);
    }

    [Fact]
    public async Task GetVocalMusicTracks_delegates_to_vocal_service()
    {
        var vocal = new ConfigurableVocalMusicService
        {
            Tracks = new SortedDictionary<int, MusicTrack>
            {
                [2] = new MusicTrack { TrackCode = "2", Title = "V2", Url = "", LookUpPath = "" },
            },
        };
        var sut = CreateSut(vocal: vocal);

        var result = await sut.GetVocalMusicTracks("E", "osg");

        Assert.Single(result);
        Assert.Equal("2", result[2].TrackCode);
    }

    [Fact]
    public async Task UpdateBiblePublicationTrackUrl_delegates_to_track_service()
    {
        var tracks = new RecordingBiblePublicationTrackService();
        var sut = CreateSut(tracks: tracks);

        await sut.UpdateBiblePublicationTrackUrl("E", "nwt", "1", "5", "https://cdn.example/a.mp3");

        Assert.Equal(("E", "nwt", "1", "5", "https://cdn.example/a.mp3"), tracks.LastUpdate);
    }

    [Fact]
    public async Task UpdateVocalTrackUrl_delegates_to_vocal_service()
    {
        var vocal = new RecordingVocalMusicService();
        var sut = CreateSut(vocal: vocal);

        await sut.UpdateVocalTrackUrl("E", "osg", "3", "https://cdn.example/v.mp3");

        Assert.Equal(("E", "osg", "3", "https://cdn.example/v.mp3"), vocal.LastUpdate);
    }

    [Fact]
    public async Task UpdateMelodyTrackUrl_delegates_to_melody_service()
    {
        var melody = new RecordingMelodyMusicService();
        var sut = CreateSut(melody: melody);

        await sut.UpdateMelodyTrackUrl("iam", "190", "https://cdn.example/m.mp3");

        Assert.Equal(("iam", "190", "https://cdn.example/m.mp3"), melody.LastUpdate);
    }

    [Fact]
    public async Task GetBiblePublicationTracks_uses_language_bound_path_when_not_no_language()
    {
        var tracks = new RecordingBiblePublicationTrackService();
        var sut = CreateSut(tracks: tracks);

        var result = await sut.GetBiblePublicationTracks("E", "nwt", "1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBiblePublications_with_progress_bypasses_cache()
    {
        var bible = new CountingPublicationsBiblePublicationService();
        var index = new CountingMediaIndexService();
        var sut = CreateSut(index: index, biblePubs: bible);
        var progress = new ImmediateFetchProgress();

        var first = await sut.GetBiblePublications("E", "Bible", downloadAll: false, progress: progress);
        var second = await sut.GetBiblePublications("E", "Bible", downloadAll: false, progress: progress);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(2, bible.GetByLanguageCodeCalls);
    }

    [Fact]
    public async Task GetBiblePublications_downloadAll_invalidates_cache_after_fetch()
    {
        var bible = new CountingPublicationsBiblePublicationService();
        var index = new CountingMediaIndexService();
        var sut = CreateSut(index: index, biblePubs: bible);

        await sut.GetBiblePublications("E", "Bible", downloadAll: true);
        await sut.GetBiblePublications("E", "Bible", downloadAll: false);

        Assert.Equal(2, bible.GetByLanguageCodeCalls);
    }

    [Fact]
    public async Task GetExpectedSectionCountAsync_returns_zero_for_empty_db()
    {
        using var db = new NoLanguagePublicationDbScope("iam-count");
        var sut = CreateSut(scopeFactory: db.Factory);

        var count = await sut.GetExpectedSectionCountAsync("E", "nwt");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetExpectedPublicationCountAsync_returns_zero_for_empty_category()
    {
        using var db = new NoLanguagePublicationDbScope("iam-count2");
        var sut = CreateSut(scopeFactory: db.Factory);

        var count = await sut.GetExpectedPublicationCountAsync("E", "Bible");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetExpectedSectionCountForNoLanguagePublicationAsync_returns_zero_when_none()
    {
        using var db = new NoLanguagePublicationDbScope("iam-count3");
        var sut = CreateSut(scopeFactory: db.Factory);

        var count = await sut.GetExpectedSectionCountForNoLanguagePublicationAsync("iam-count3");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetVocalMusicReleases_returns_helper_result()
    {
        var vocal = new ConfigurableVocalMusicService
        {
            Releases = new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase)
            {
                ["osg"] = new BiblePublication { Id = 1, PublicationCode = "osg", Name = "Sing Out" },
            },
        };
        var bible = new CountingPublicationsBiblePublicationService();
        var sut = CreateSut(biblePubs: bible, vocal: vocal);

        var result = await sut.GetVocalMusicReleases("E");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetBiblePublicationSections_for_english_skips_ensure_all_sections()
    {
        var sections = new StubBiblePublicationSectionService
        {
            ByPublicationResult = new SortedDictionary<string, BiblePublicationSection>
            {
                ["1"] = new() { SectionCode = "1", Name = "Genesis" },
            },
        };
        var sut = CreateSut(sections: sections);

        var result = await sut.GetBiblePublicationSections("E", "nwt", progress: new ImmediateFetchProgress());

        Assert.Single(result);
        Assert.Equal(1, sections.GetSectionsByPublicationCallCount);
    }

    private sealed class ConfigurableMelodyMusicService : IMelodyMusicService
    {
        public SortedDictionary<int, MusicTrack> TracksByCode { get; set; } = [];
        public SortedDictionary<int, MusicTrack> TracksBySection { get; set; } = [];

        public void Dispose()
        {
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<MelodyMusic?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(TracksByCode);

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(TracksBySection);

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ConfigurableVocalMusicService : IVocalMusicService
    {
        public SortedDictionary<int, MusicTrack> Tracks { get; set; } = [];
        public Dictionary<string, VocalMusic> Releases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
        }

        public Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<VocalMusic?>(null);

        public Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Releases);

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tracks);

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ImmediateFetchProgress : IFetchProgress
    {
        public CancellationToken CancellationToken => CancellationToken.None;

        public void UpdateProgress(double progress)
        {
        }

        public void UpdateProgressText(string text)
        {
        }

        public void SetIsVisible(bool isVisible)
        {
        }
    }
}
