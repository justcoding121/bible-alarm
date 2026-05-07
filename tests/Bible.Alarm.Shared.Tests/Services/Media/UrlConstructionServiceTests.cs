#nullable enable

using System.Collections;
using System.Reflection;
using System.Threading;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Shared.Tests;

public sealed class UrlConstructionServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private sealed class CountingScopeFactory(MediaTestScopeFactory inner) : IServiceScopeFactory
    {
        private int createScopeCallCount;

        public int CreateScopeCallCount => Volatile.Read(ref createScopeCallCount);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref createScopeCallCount);
            return inner.CreateScope();
        }
    }

    private DbContextOptions<MediaDbContext> Options =>
        new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

    public Task InitializeAsync()
    {
        connection.Open();
        using var bootstrap = new MediaDbContext(Options);
        bootstrap.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public async Task ConstructTrackUrlsAsync_ById_ReturnsEmpty_WhenMissing()
    {
        var sut = CreateSut();

        Assert.Empty(await sut.ConstructTrackUrlsAsync(trackId: 999));
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ById_ReturnsEmpty_WhenUrlMissing()
    {
        await using var db = new MediaDbContext(Options);
        var track = await SeedTrackWithLanguageSectionAsync(db, publicationCode: "pcb-blank-url", url: "");

        var sut = CreateSut();

        Assert.Empty(await sut.ConstructTrackUrlsAsync(track.Id));
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ById_ReturnsStoredUrl_WhenPresent()
    {
        await using var db = new MediaDbContext(Options);
        var track = await SeedTrackWithLanguageSectionAsync(db, publicationCode: "pcb-byid-url", url: "https://cdn/track.mp3");

        var sut = CreateSut();

        var urls = await sut.ConstructTrackUrlsAsync(track.Id);

        Assert.Single(urls);
        Assert.Equal("https://cdn/track.mp3", urls[0]);
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ByCodes_MatchesPublicationLanguage_WithCaseFoldedInputs()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-casefold";
        var lang = DerivedLanguage(publicationCode);
        var track = await SeedTrackWithLanguageSectionAsync(
            db,
            publicationCode,
            sectionCodeStored: "mat-1",
            trackCode: "2",
            url: "https://u/b");

        var sut = CreateSut();

        var urls = await sut.ConstructTrackUrlsAsync(
            publicationCode,
            languageCode: lang.ToLowerInvariant(),
            sectionCode: "MAT-1",
            trackCode: "2");

        Assert.Single(urls);
        Assert.Equal("https://u/b", urls[0]);
        Assert.True(track.Id > 0);
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ByCodes_ReturnsEmpty_When_LanguageDoesNotMatchPublication()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-lang-mismatch";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode, url: "https://only-for-lang-rows");

        var sut = CreateSut();

        Assert.Empty(await sut.ConstructTrackUrlsAsync(publicationCode, languageCode: "ZZZZ", sectionCode: "mat-1", trackCode: "1"));
        Assert.Single(await sut.ConstructTrackUrlsAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "1"));
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ByCodes_NoLanguagePublication_IgnoresLanguageFilter()
    {
        await using var db = new MediaDbContext(Options);
        var track = await SeedInstrumentalPublicationTrackAsync(db, publicationCode: "pcb-nolang", url: "https://melody.only");

        var sut = CreateSut();

        var urls = await sut.ConstructTrackUrlsAsync("pcb-nolang", languageCode: "XX", sectionCode: null, trackCode: "10");

        Assert.Single(urls);
        Assert.Equal("https://melody.only", urls[0]);
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ByCodes_SectionMustBeAbsent_ForPublicationLevelTrack()
    {
        await using var db = new MediaDbContext(Options);
        await SeedInstrumentalPublicationTrackAsync(db, publicationCode: "pcb-flat-sec", url: "https://flat.track");

        var sut = CreateSut();

        Assert.Empty(await sut.ConstructTrackUrlsAsync("pcb-flat-sec", "E", sectionCode: "any", trackCode: "10"));
        Assert.Single(await sut.ConstructTrackUrlsAsync("pcb-flat-sec", "E", sectionCode: null, trackCode: "10"));
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ByCodes_NullTrackCode_MatchesEmptyStoredTrackCode()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-null-track-arg";
        var track = await SeedTrackWithLanguageSectionAsync(
            db,
            publicationCode,
            sectionCodeStored: "sec-a",
            trackCode: "",
            url: "https://empty-code-match");

        var sut = CreateSut();
        var lang = DerivedLanguage(publicationCode);

        var urls = await sut.ConstructTrackUrlsAsync(publicationCode, lang, sectionCode: "sec-a", trackCode: null!);

        Assert.Single(urls);
        Assert.Equal("https://empty-code-match", urls[0]);
        Assert.True(track.Id > 0);
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_ReturnsNull_WhenNoRow()
    {
        var sut = CreateSut();

        Assert.Null(await sut.ConstructTrackLookUpPathAsync("nop", "E", "1", "1"));
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_ReturnsUrl_WhenRowExists()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-lookup";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode: publicationCode, url: "https://look/path");

        var sut = CreateSut();

        var path = await sut.ConstructTrackLookUpPathAsync(
            publicationCode,
            lang.ToLowerInvariant(),
            sectionCode: "mat-1",
            trackCode: "1");

        Assert.Equal("https://look/path", path);
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_NoLangPublicationTreatsEffectiveLanguage_AsNullLanguage()
    {
        await using var db = new MediaDbContext(Options);
        await SeedInstrumentalPublicationTrackAsync(db, publicationCode: "pcb-look-nolang", url: "https://in");

        var sut = CreateSut();

        var path = await sut.ConstructTrackLookUpPathAsync("pcb-look-nolang", languageCode: "JP", sectionCode: null, trackCode: "10");

        Assert.Equal("https://in", path);
    }

    [Fact]
    public async Task ClearLookUpPathCache_AllowsReuse_AfterManualClear_NoExplosionWhenSecondLookupSameUrl()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-cache-clear";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode: publicationCode, url: "https://z");

        var sut = CreateSut();

        Assert.Equal(
            "https://z",
            await sut.ConstructTrackLookUpPathAsync(
                publicationCode,
                lang.ToLowerInvariant(),
                sectionCode: "mat-1",
                trackCode: "1"));
        sut.ClearLookUpPathCache();
        Assert.Equal(
            "https://z",
            await sut.ConstructTrackLookUpPathAsync(
                publicationCode,
                lang.ToLowerInvariant(),
                sectionCode: "mat-1",
                trackCode: "1"));
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_ReQueriesDatabase_After_CacheEntry_AgedPast_Ttl()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-lookup-ttl";
        var lang = DerivedLanguage(publicationCode);
        var track = await SeedTrackWithLanguageSectionAsync(db, publicationCode, url: "https://ttl-first");

        var sut = CreateSut();

        Assert.Equal(
            "https://ttl-first",
            await sut.ConstructTrackLookUpPathAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "1"));

        await using (var edit = new MediaDbContext(Options))
        {
            var urlRow = await edit.TrackUrls.SingleAsync(u => u.BiblePublicationTrackId == track.Id);
            urlRow.Url = "https://ttl-second";
            await edit.SaveChangesAsync();
        }

        Assert.Equal(
            "https://ttl-first",
            await sut.ConstructTrackLookUpPathAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "1"));

        StampLookupPathCacheCreatedAtUtc(sut, DateTimeOffset.UtcNow.AddMinutes(-6));

        Assert.Equal(
            "https://ttl-second",
            await sut.ConstructTrackLookUpPathAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "1"));
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_Retries_After_Transient_ScopeFailure_Evicts_CacheEntry()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-cache-retry";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode: publicationCode, url: "https://recover");

        var inner = new MediaTestScopeFactory(Options);
        var sut = new UrlConstructionService(new FailFirstScopeThenInnerFactory(inner));

        await Assert.ThrowsAsync<DivideByZeroException>(() =>
            sut.ConstructTrackLookUpPathAsync(
                publicationCode,
                lang.ToLowerInvariant(),
                sectionCode: "mat-1",
                trackCode: "1"));

        Assert.Equal(
            "https://recover",
            await sut.ConstructTrackLookUpPathAsync(
                publicationCode,
                lang.ToLowerInvariant(),
                sectionCode: "mat-1",
                trackCode: "1"));
    }

    [Fact]
    public async Task ConstructTrackUrlsAsync_ByCodes_ReturnsEmpty_When_TrackCode_CaseMismatch()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-tcode-case";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode, trackCode: "Ab", url: "https://case/track");

        var sut = CreateSut();

        Assert.Empty(await sut.ConstructTrackUrlsAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "ab"));
        Assert.Single(await sut.ConstructTrackUrlsAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "Ab"));
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_ReturnsNull_When_TrackCodeCaseMismatch()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-lookup-tcode-case";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode, trackCode: "Zz", url: "https://z-only");

        var sut = CreateSut();

        Assert.Null(await sut.ConstructTrackLookUpPathAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "zz"));
        Assert.Equal(
            "https://z-only",
            await sut.ConstructTrackLookUpPathAsync(publicationCode, lang, sectionCode: "mat-1", trackCode: "Zz"));
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_CacheKey_NormalizesLanguageAndSectionCase_ReusesLazy_SingleDbScope()
    {
        await using var db = new MediaDbContext(Options);
        const string publicationCode = "pcb-look-cc";
        var lang = DerivedLanguage(publicationCode);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode: publicationCode, url: "https://one-scope");

        var inner = new MediaTestScopeFactory(Options);
        var counting = new CountingScopeFactory(inner);
        var sut = new UrlConstructionService(counting);

        var first = await sut.ConstructTrackLookUpPathAsync(
            publicationCode,
            lang.ToLowerInvariant(),
            sectionCode: "MAT-1",
            trackCode: "1");

        var second = await sut.ConstructTrackLookUpPathAsync(
            publicationCode,
            lang.ToUpperInvariant(),
            sectionCode: "mat-1",
            trackCode: "1");

        Assert.Equal("https://one-scope", first);
        Assert.Equal("https://one-scope", second);
        Assert.Equal(1, counting.CreateScopeCallCount);
    }

    [Fact]
    public async Task ConstructTrackLookUpPathAsync_DistinctPublicationKeys_UseSeparateLazyEntries_TwoDbScopes()
    {
        await using var db = new MediaDbContext(Options);
        await SeedTrackWithLanguageSectionAsync(db, publicationCode: "pcb-s1", url: "https://s1");
        await SeedTrackWithLanguageSectionAsync(db, publicationCode: "pcb-s2", url: "https://s2");

        var inner = new MediaTestScopeFactory(Options);
        var counting = new CountingScopeFactory(inner);
        var sut = new UrlConstructionService(counting);

        var lang1 = DerivedLanguage("pcb-s1");
        var lang2 = DerivedLanguage("pcb-s2");

        Assert.Equal(
            "https://s1",
            await sut.ConstructTrackLookUpPathAsync("pcb-s1", lang1, sectionCode: "mat-1", trackCode: "1"));
        Assert.Equal(
            "https://s2",
            await sut.ConstructTrackLookUpPathAsync("pcb-s2", lang2, sectionCode: "mat-1", trackCode: "1"));

        Assert.Equal(2, counting.CreateScopeCallCount);
    }

    /// <summary>Deterministic codes ≤10 chars, distinct across varied publication prefixes in these tests.</summary>
    private static string DerivedLanguage(string publicationCode)
    {
        var collapsed = publicationCode.Replace("-", "", StringComparison.Ordinal);
        if (collapsed.Length < 2)
        {
            collapsed += "xx";
        }

        var upper = collapsed.ToUpperInvariant();
        return upper.Length <= 10 ? upper : upper[..10];
    }

    private UrlConstructionService CreateSut() => new(new MediaTestScopeFactory(Options));

    /// <summary>
    /// Forces cache entries older than <see cref="UrlConstructionService" /> TTL (5 minutes) without waiting.
    /// </summary>
    private static void StampLookupPathCacheCreatedAtUtc(UrlConstructionService sut, DateTimeOffset createdAtUtc)
    {
        var serviceType = typeof(UrlConstructionService);
        var cacheField = serviceType.GetField("lookUpPathCache", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field lookUpPathCache on {serviceType.FullName}");

        var dict = cacheField.GetValue(sut);
        Assert.NotNull(dict);

        var entryType = serviceType.GetNestedType("LookUpPathCacheEntry", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing nested type LookUpPathCacheEntry on {serviceType.FullName}");

        var createdAtField =
            entryType.GetField("<CreatedAt>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? entryType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Single(static f =>
                f.FieldType == typeof(DateTimeOffset));

        var sawEntry = false;
        foreach (var kv in (IEnumerable)dict)
        {
            sawEntry = true;
            var value = kv.GetType().GetProperty("Value")!.GetValue(kv)!;
            createdAtField.SetValue(value, createdAtUtc);
        }

        Assert.True(sawEntry, "Expected at least one lookup-path cache entry to stamp.");
    }

    private sealed class FailFirstScopeThenInnerFactory(MediaTestScopeFactory inner) : Microsoft.Extensions.DependencyInjection.IServiceScopeFactory
    {
        private int scopeSequence;

        public Microsoft.Extensions.DependencyInjection.IServiceScope CreateScope()
        {
            if (Interlocked.Increment(ref scopeSequence) == 1)
            {
                throw new DivideByZeroException("simulated transient scope failure");
            }

            return inner.CreateScope();
        }
    }

    private static async Task<BiblePublicationTrack> SeedTrackWithLanguageSectionAsync(
        MediaDbContext db,
        string publicationCode,
        string sectionCodeStored = "mat-1",
        string trackCode = "1",
        string url = "https://example/a.mp3")
    {
        var languageCode = DerivedLanguage(publicationCode);
        var lang = new Language
        {
            LanguageCode = languageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var pub = new BiblePublication
        {
            Name = "Test Publication",
            PublicationCode = publicationCode,
            LanguageId = lang.Id,
            Language = lang,
            IsVideo = false,
            IsMusic = false,
        };

        var section = new BiblePublicationSection
        {
            Name = "Section One",
            SectionCode = sectionCodeStored,
            BiblePublication = pub,
        };
        pub.Sections.Add(section);

        var track = new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = "Track Title",
            Publication = pub,
            Section = section,
        };
        section.Tracks.Add(track);

        db.BiblePublications.Add(pub);
        await db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(url))
        {
            track.TrackUrl = new TrackUrl
            {
                Url = url,
                BiblePublicationTrack = track,
            };
            db.TrackUrls.Add(track.TrackUrl);
            await db.SaveChangesAsync();
        }

        return track;
    }

    private static async Task<BiblePublicationTrack> SeedInstrumentalPublicationTrackAsync(
        MediaDbContext db,
        string publicationCode = "mel-only",
        string trackCode = "10",
        string url = "https://melody/track")
    {
        var pub = new BiblePublication
        {
            Name = "Instrumental Pub",
            PublicationCode = publicationCode,
            LanguageId = null,
            Language = null,
            IsVideo = false,
            IsMusic = true,
        };

        var track = new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = "M1",
            Publication = pub,
            Section = null,
            BiblePublicationSectionId = null,
        };

        pub.Tracks.Add(track);
        db.BiblePublications.Add(pub);
        await db.SaveChangesAsync();

        track.TrackUrl = new TrackUrl
        {
            Url = url,
            BiblePublicationTrack = track,
        };
        db.TrackUrls.Add(track.TrackUrl);
        await db.SaveChangesAsync();

        return track;
    }
}
