#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationEnsurerAllPublicationsEnsurerTests
{
    private static async Task<(MediaTestScopeFactory Factory, SqliteConnection Connection)> CreateFactoryAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        return (new MediaTestScopeFactory(options), connection);
    }

    private sealed class CaptureProgress : IFetchProgress
    {
        public List<double> ProgressValues { get; } = [];
        public bool VisibleSetTrue { get; private set; }
        public CancellationToken CancellationToken { get; }

        public CaptureProgress(CancellationToken cancellationToken = default) =>
            CancellationToken = cancellationToken;

        public void UpdateProgress(double progress) => ProgressValues.Add(progress);

        public void UpdateProgressText(string text) { }

        public void SetIsVisible(bool isVisible)
        {
            if (isVisible)
            {
                VisibleSetTrue = true;
            }
        }
    }

    private static Task<bool> StubEnsureNeverCalled() =>
        throw new InvalidOperationException("ensurePublicationExists should not be invoked.");

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Null()
        => Assert.Throws<ArgumentNullException>(() => new PublicationEnsurerAllPublicationsEnsurer(
            null!, TestLogging.CreateLogger(), (pub, lang, prog, ct) => StubEnsureNeverCalled()));

    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        Task<bool> Stub(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken ct) =>
            Task.FromResult(true);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
        using (var bootstrap = new MediaDbContext(opts))
        {
            bootstrap.Database.EnsureCreated();
        }

        Assert.Throws<ArgumentNullException>(() => new PublicationEnsurerAllPublicationsEnsurer(
            new MediaTestScopeFactory(opts), null!, Stub));
    }

    [Fact]
    public void Constructor_Throws_When_EnsureDelegate_Null()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        try
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            using (var init = new MediaDbContext(opts))
            {
                init.Database.EnsureCreated();
            }

            Assert.Throws<ArgumentNullException>(() => new PublicationEnsurerAllPublicationsEnsurer(
                new MediaTestScopeFactory(opts), TestLogging.CreateLogger(), null!));
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task Ensure_Skips_Fetch_When_Language_Is_Default_English_Code()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory,
                TestLogging.CreateLogger(),
                (pubCode, lc, prog, ct) => StubEnsureNeverCalled());

            var ok = await sut.EnsureAllPublicationsForLanguageAsync(
                AppConstants.Media.DefaultLanguageCode.ToLowerInvariant());

            Assert.True(ok);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsTrue_When_NoPublications_missing_For_Language()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var langFx = new Language
                {
                    LanguageCode = "FX",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsPubCatFx" };

                seed.Languages.Add(langFx);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "ens-has-both",
                    Category = cat,
                    Language = langFx,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "Has both",
                    PublicationCode = "ens-has-both",
                    LanguageId = langFx.Id,
                    Language = langFx,
                    IsVideo = false,
                    IsMusic = false,
                });

                await seed.SaveChangesAsync();
            }

            var invoked = false;
            Task<bool> EnsurePub(string pc, string lc, IFetchProgress? prog, CancellationToken ct)
            {
                invoked = true;
                return Task.FromResult(false);
            }

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            var ok = await sut.EnsureAllPublicationsForLanguageAsync("fx");

            Assert.False(invoked);
            Assert.True(ok);
        }
    }

    [Fact]
    public async Task Ensure_Calls_delegate_And_returns_true_When_missing_publications_succeed_once()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var langFx = new Language
                {
                    LanguageCode = "FY",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsPubCatFy" };

                seed.Languages.Add(langFx);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "only-pl",
                    Category = cat,
                    Language = langFx,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });

                await seed.SaveChangesAsync();
            }

            var calls = new List<string>();
            Task<bool> EnsurePub(string pc, string lc, IFetchProgress? prog, CancellationToken ct)
            {
                calls.Add(pc + "/" + lc);
                return Task.FromResult(true);
            }

            using var cts = new CancellationTokenSource();
            var progress = new CaptureProgress(cts.Token);

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            var ok = await sut.EnsureAllPublicationsForLanguageAsync(
                "fy", categoryName: null, progress: progress, cts.Token);

            Assert.True(ok);
            Assert.Single(calls);
            Assert.Equal("only-pl/fy", calls[0], StringComparer.OrdinalIgnoreCase);
            Assert.True(progress.VisibleSetTrue);
            Assert.Contains(1.0, progress.ProgressValues);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsFalse_When_missing_publications_never_succeed()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var langFx = new Language
                {
                    LanguageCode = "FZ",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsPubCatFz" };

                seed.Languages.Add(langFx);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "fail-pl",
                    Category = cat,
                    Language = langFx,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });

                await seed.SaveChangesAsync();
            }

            Task<bool> EnsurePub(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken ct) =>
                Task.FromResult(false);

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            var ok = await sut.EnsureAllPublicationsForLanguageAsync("fz");

            Assert.False(ok);
        }
    }
}
