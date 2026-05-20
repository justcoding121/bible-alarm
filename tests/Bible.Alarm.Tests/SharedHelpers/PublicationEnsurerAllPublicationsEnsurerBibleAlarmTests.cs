#nullable enable

using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace Bible.Alarm.Tests;

public sealed class PublicationEnsurerAllPublicationsEnsurerBibleAlarmTests
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
        public bool VisibleSetFalse { get; private set; }
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
            else
            {
                VisibleSetFalse = true;
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

    [Fact]
    public async Task Ensure_ReturnsFalse_When_Delegate_Throws_Generic_Exception()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var langGq = new Language
                {
                    LanguageCode = "GQ",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsPubThrowCat" };

                seed.Languages.Add(langGq);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "throw-pl",
                    Category = cat,
                    Language = langGq,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });

                await seed.SaveChangesAsync();
            }

            Task<bool> Boom(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken ct) =>
                throw new InvalidOperationException("delegated fetch failure simulation");

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), Boom);

            Assert.False(await sut.EnsureAllPublicationsForLanguageAsync("gq"));
        }
    }

    [Fact]
    public async Task Ensure_Filters_Publications_By_Category_When_CategoryName_Provided()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KH",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var bible = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var music = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                seed.Languages.Add(lang);
                seed.Categories.AddRange(bible, music);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.AddRange(
                    new PublicationLanguage
                    {
                        PublicationCode = "cat-filter-bible-only",
                        Category = bible,
                        Language = lang,
                        IsMusic = false,
                        CatalogType = CatalogType.Flat,
                    },
                    new PublicationLanguage
                    {
                        PublicationCode = "cat-filter-music-only",
                        Category = music,
                        Language = lang,
                        IsMusic = true,
                        CatalogType = CatalogType.Flat,
                    });
                await seed.SaveChangesAsync();
            }

            var calls = new List<string>();
            Task<bool> EnsurePub(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken ct)
            {
                calls.Add(publicationCode);
                return Task.FromResult(false);
            }

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            await sut.EnsureAllPublicationsForLanguageAsync(
                "kh",
                AppConstants.Media.BiblePublicationCategoryMusic);

            Assert.Single(calls);
            Assert.Equal("cat-filter-music-only", calls[0]);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsTrue_When_AtLeast_One_missing_publication_Succeeds()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KI",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsPartialCatKi" };
                seed.Languages.Add(lang);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                foreach (var code in new[] { "partial-a-pl", "partial-b-pl" })
                {
                    seed.PublicationLanguages.Add(new PublicationLanguage
                    {
                        PublicationCode = code,
                        Category = cat,
                        Language = lang,
                        IsMusic = false,
                        CatalogType = CatalogType.Flat,
                    });
                }

                await seed.SaveChangesAsync();
            }

            var calls = new List<string>();
            Task<bool> EnsurePub(string publicationCode, string langCodeIgnored, IFetchProgress? progIgnored, CancellationToken ct)
            {
                calls.Add(publicationCode);
                return Task.FromResult(string.Equals(publicationCode, "partial-a-pl", StringComparison.Ordinal));
            }

            var progress = new CaptureProgress();
            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            var ok = await sut.EnsureAllPublicationsForLanguageAsync("ki", progress: progress);

            Assert.True(ok);
            Assert.Equal(2, calls.Count);
            Assert.Contains("partial-a-pl", calls);
            Assert.Contains("partial-b-pl", calls);
            Assert.Contains(1.0, progress.ProgressValues);
            Assert.True(progress.VisibleSetFalse);
        }
    }

    [Fact]
    public async Task Ensure_Uses_Progress_PerItem_Fractions_From_Zero_through_One_When_three_missing()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KJ",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsTripletCat" };
                seed.Languages.Add(lang);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                foreach (var code in new[] { "triple-1-pl", "triple-2-pl", "triple-3-pl" })
                {
                    seed.PublicationLanguages.Add(new PublicationLanguage
                    {
                        PublicationCode = code,
                        Category = cat,
                        Language = lang,
                        IsMusic = false,
                        CatalogType = CatalogType.Flat,
                    });
                }

                await seed.SaveChangesAsync();
            }

            var progress = new CaptureProgress();

            Task<bool> EnsurePub(string publicationCodeIgnored, string languageCodeIgnored, IFetchProgress? progIgnored, CancellationToken ct) =>
                Task.FromResult(true);

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            Assert.True(await sut.EnsureAllPublicationsForLanguageAsync("kj", progress: progress));

            Assert.Contains(1.0 / 3, progress.ProgressValues);
            Assert.Contains(2.0 / 3, progress.ProgressValues);
            Assert.Contains(1.0, progress.ProgressValues);
        }
    }

    [Fact]
    public async Task Ensure_Rethrows_OperationCanceledException_And_Hides_Progress_When_progress_token_Canceled_Before_LOOP()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KK",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsCancelCat" };
                seed.Languages.Add(lang);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "cancel-pl",
                    Category = cat,
                    Language = lang,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });
                await seed.SaveChangesAsync();
            }

            Task<bool> Never(string publicationCodeIgnored, string languageCodeIgnored, IFetchProgress? progIgnored, CancellationToken ct) =>
                Task.FromResult(true);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var progress = new CaptureProgress(cts.Token);
            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), Never);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sut.EnsureAllPublicationsForLanguageAsync(
                    "kk",
                    categoryName: null,
                    progress: progress,
                    cancellationToken: CancellationToken.None));

            Assert.True(progress.VisibleSetFalse);
        }
    }

    [Fact]
    public async Task Ensure_Rethrows_HttpRequestException_From_Delegate_and_Hides_Progress()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KL",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsHttpCat" };
                seed.Languages.Add(lang);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "http-pl",
                    Category = cat,
                    Language = lang,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });
                await seed.SaveChangesAsync();
            }

            Task<bool> ThrowHttp(string publicationCodeIgnored, string languageCodeIgnored, IFetchProgress? progIgnored, CancellationToken ct) =>
                Task.FromException<bool>(new HttpRequestException("simulated"));

            var progress = new CaptureProgress();
            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), ThrowHttp);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.EnsureAllPublicationsForLanguageAsync("kl", progress: progress));

            Assert.True(progress.VisibleSetFalse);
        }
    }

    [Fact]
    public async Task Ensure_Rethrows_SocketException_From_Delegate_and_Hides_Progress()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KM",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsSockCat" };
                seed.Languages.Add(lang);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "sock-pl",
                    Category = cat,
                    Language = lang,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });
                await seed.SaveChangesAsync();
            }

            Task<bool> ThrowSock(string publicationCodeIgnored, string languageCodeIgnored, IFetchProgress? progIgnored, CancellationToken ct) =>
                Task.FromException<bool>(new SocketException(99));

            var progress = new CaptureProgress();
            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), ThrowSock);

            await Assert.ThrowsAsync<SocketException>(() =>
                sut.EnsureAllPublicationsForLanguageAsync("km", progress: progress));

            Assert.True(progress.VisibleSetFalse);
        }
    }

    [Fact]
    public async Task Ensure_Does_Not_Call_delegate_When_Publication_Already_In_Db_With_Different_String_Casing()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "KN",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var cat = new Category { CategoryCode = "EnsCaseCat" };
                seed.Languages.Add(lang);
                seed.Categories.Add(cat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "CasePublicationZ",
                    Category = cat,
                    Language = lang,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                });
                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "Matched",
                    PublicationCode = "casepublicationz",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                });

                await seed.SaveChangesAsync();
            }

            var invoked = false;
            Task<bool> EnsurePub(string publicationCodeIgnored, string languageCodeIgnored, IFetchProgress? progIgnored, CancellationToken ctIgnored)
            {
                invoked = true;
                return Task.FromResult(true);
            }

            var sut = new PublicationEnsurerAllPublicationsEnsurer(
                factory, TestLogging.CreateLogger(), EnsurePub);

            Assert.True(await sut.EnsureAllPublicationsForLanguageAsync("kn"));

            Assert.False(invoked);
        }
    }
}