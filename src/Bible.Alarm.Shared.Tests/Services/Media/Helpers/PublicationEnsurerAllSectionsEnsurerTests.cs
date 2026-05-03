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
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationEnsurerAllSectionsEnsurerTests
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

    private sealed class StubFetchSections(ILanguageContentService inner) : ILanguageContentService
    {
        public List<(string Pub, string Lang, IFetchProgress? Progress)> FetchCalls { get; } = [];
        public Func<string, string, IFetchProgress?, CancellationToken, Task<bool>> FetchHandler { get; set; } =
            (_, __, ___, ct) => Task.FromResult(false);

        public Task<bool> FetchPublicationSectionsAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken)
        {
            FetchCalls.Add((publicationCode, languageCode, progress));
            return FetchHandler(publicationCode, languageCode, progress, cancellationToken);
        }

        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode, CancellationToken cancellationToken) =>
            inner.GetVideoPublicationDisplayNameAsync(publicationCode, languageCode, cancellationToken);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken) =>
            inner.FetchPublicationTracksAsync(publicationCode, languageCode, cancellationToken);

        public Task<bool> FetchSectionTracksAsync(
            string publicationCode,
            string sectionCode,
            string languageCode,
            bool replaceExistingTracksFromApi,
            CancellationToken cancellationToken) =>
            inner.FetchSectionTracksAsync(publicationCode, sectionCode, languageCode, replaceExistingTracksFromApi, cancellationToken);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken) =>
            inner.SeedEnglishPublicationAsync(publicationCode, cancellationToken);

        public Task<bool> EnsurePublicationExistsAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            inner.EnsurePublicationExistsAsync(publicationCode, languageCode, progress, cancellationToken);

        public Task<bool> FetchFirstPublicationForLanguageAsync(
            string languageCode,
            string? categoryName,
            CancellationToken cancellationToken) =>
            inner.FetchFirstPublicationForLanguageAsync(languageCode, categoryName, cancellationToken);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(
            string languageCode,
            string? categoryName,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            inner.EnsureAllPublicationsForLanguageAsync(languageCode, categoryName, progress, cancellationToken);

        public Task<bool> EnsureAllSectionsForPublicationAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            inner.EnsureAllSectionsForPublicationAsync(publicationCode, languageCode, progress, cancellationToken);

        public Task<bool> FetchFirstSectionOnlyAsync(
            string publicationCode,
            string firstSectionCode,
            string languageCode,
            CancellationToken cancellationToken) =>
            inner.FetchFirstSectionOnlyAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
    }

    private sealed class UnusedLanguageServices : ILanguageContentService
    {
        private static InvalidOperationException Never(string name) =>
            new($"{name} was not stubbed or expected.");

        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode, CancellationToken cancellationToken) =>
            throw Never(nameof(GetVideoPublicationDisplayNameAsync));

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken) =>
            throw Never(nameof(FetchPublicationTracksAsync));

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken cancellationToken) =>
            throw Never(nameof(FetchPublicationSectionsAsync));

        public Task<bool> FetchSectionTracksAsync(
            string publicationCode,
            string sectionCode,
            string languageCode,
            bool replaceExistingTracksFromApi,
            CancellationToken cancellationToken) =>
            throw Never(nameof(FetchSectionTracksAsync));

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken) =>
            throw Never(nameof(SeedEnglishPublicationAsync));

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken cancellationToken) =>
            throw Never(nameof(EnsurePublicationExistsAsync));

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName, CancellationToken cancellationToken) =>
            throw Never(nameof(FetchFirstPublicationForLanguageAsync));

        public Task<bool> EnsureAllPublicationsForLanguageAsync(
            string languageCode,
            string? categoryName,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            throw Never(nameof(EnsureAllPublicationsForLanguageAsync));

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode, IFetchProgress? progress, CancellationToken cancellationToken) =>
            throw Never(nameof(EnsureAllSectionsForPublicationAsync));

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode, CancellationToken cancellationToken) =>
            throw Never(nameof(FetchFirstSectionOnlyAsync));
    }

    private sealed class ProgressSpy : IFetchProgress
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public bool SawVisibleTrue { get; private set; }
        public bool SawVisibleFalse { get; private set; }

        public void SetIsVisible(bool isVisible)
        {
            if (isVisible)
            {
                SawVisibleTrue = true;
            }
            else
            {
                SawVisibleFalse = true;
            }
        }

        public void UpdateProgress(double progress) { }

        public void UpdateProgressText(string text) { }
    }

    private const string TestPubCode = "secw-pub-01";    private static readonly string LangCodeSections = "HS";

    private static async Task SeedPublicationWithSectionsAsync(
        MediaDbContext db,
        bool secondSectionListed,
        bool secondSectionStored)
    {
        var language = new Language
        {
            LanguageCode = LangCodeSections,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var category = new Category { CategoryCode = "CatSecEnsurer" };

        db.Languages.Add(language);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var publicationLanguage = new PublicationLanguage
        {
            PublicationCode = TestPubCode,
            Category = category,
            Language = language,
            IsMusic = false,
            CatalogType = CatalogType.Flat,
        };

        db.PublicationLanguages.Add(publicationLanguage);
        await db.SaveChangesAsync();

        void AddSectionLanguage(string sectionCode)
        {
            db.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = TestPubCode,
                SectionCode = sectionCode,
                LanguageId = language.Id,
                Language = language,
                PublicationLanguageId = publicationLanguage.Id,
                PublicationLanguage = publicationLanguage,
            });
        }

        AddSectionLanguage("sw-a");
        if (secondSectionListed)
        {
            AddSectionLanguage("sw-b");
        }

        await db.SaveChangesAsync();

        var biblePublication = new BiblePublication
        {
            Name = "Section wave test",
            PublicationCode = TestPubCode,
            LanguageId = language.Id,
            Language = language,
            IsVideo = false,
            IsMusic = false,
        };

        db.BiblePublicationSections.Add(new BiblePublicationSection
        {
            Name = "A",
            SectionCode = "sw-a",
            BiblePublication = biblePublication,
        });

        if (secondSectionStored)
        {
            db.BiblePublicationSections.Add(new BiblePublicationSection
            {
                Name = "B",
                SectionCode = "sw-b",
                BiblePublication = biblePublication,
            });
        }

        db.BiblePublications.Add(biblePublication);
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Is_Null()
    {
        var unused = new UnusedLanguageServices();
        var svc = new StubFetchSections(unused);
        Assert.Throws<ArgumentNullException>(() => new PublicationEnsurerAllSectionsEnsurer(
            null!, TestLogging.CreateLogger(), svc));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
        using (var bootstrap = new MediaDbContext(opts))
        {
            bootstrap.Database.EnsureCreated();
        }

        var unused = new UnusedLanguageServices();
        var svc = new StubFetchSections(unused);

        Assert.Throws<ArgumentNullException>(() => new PublicationEnsurerAllSectionsEnsurer(
            new MediaTestScopeFactory(opts), null!, svc));
    }

    [Fact]
    public void Constructor_Throws_When_LanguageService_Is_Null()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
        using (var bootstrap = new MediaDbContext(opts))
        {
            bootstrap.Database.EnsureCreated();
        }

        Assert.Throws<ArgumentNullException>(() => new PublicationEnsurerAllSectionsEnsurer(
            new MediaTestScopeFactory(opts), TestLogging.CreateLogger(), null!));
    }

    [Fact]
    public async Task Ensure_ReturnsFalse_When_Publication_Is_Not_In_Database()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (pubCode, lc, prog, ct) => throw new InvalidOperationException("Fetch should not run."),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            var ok = await sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections);

            Assert.False(ok);
            Assert.Empty(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsTrue_When_All_SectionLanguages_Already_Loaded_As_Sections()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: false, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (pubCode, lc, prog, ct) => throw new InvalidOperationException("Fetch should not run."),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            var ok = await sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections);

            Assert.True(ok);
            Assert.Empty(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_Delegates_To_LanguageService_When_Gaps_Remain_And_reports_progress()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: true, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (pubCode, lc, prog, ct) => Task.FromResult(true),
            };

            var progress = new ProgressSpy();

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            var ok = await sut.EnsureAllSectionsForPublicationAsync(
                publicationCode: TestPubCode,
                languageCode: LangCodeSections.ToLowerInvariant(),
                progress: progress);

            Assert.True(ok);
            Assert.Single(stub.FetchCalls);
            Assert.Equal(TestPubCode, stub.FetchCalls[0].Pub);
            Assert.Equal(LangCodeSections, stub.FetchCalls[0].Lang, StringComparer.OrdinalIgnoreCase);
            Assert.Same(progress, stub.FetchCalls[0].Progress);
            Assert.True(progress.SawVisibleTrue);
            Assert.True(progress.SawVisibleFalse);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsFalse_When_Fetch_Returns_false()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: true, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (pubCode, lc, prog, ct) => Task.FromResult(false),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            var ok = await sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections);

            Assert.False(ok);
            Assert.Single(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsFalse_When_Fetch_Propagates_As_Generic_Exception()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: true, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (_, _, _, _) =>
                    Task.FromException<bool>(new InvalidOperationException("simulated downstream failure")),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            var ok = await sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections);

            Assert.False(ok);
            Assert.Single(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_Rethrows_HttpRequestException_From_Fetch()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: true, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (_, _, _, _) =>
                    Task.FromException<bool>(new HttpRequestException("simulated")),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections));
            Assert.Single(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_Rethrows_SocketException_From_Fetch()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: true, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (_, _, _, _) =>
                    Task.FromException<bool>(new SocketException(111)),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<SocketException>(() =>
                sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections));
            Assert.Single(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_Rethrows_OperationCanceledException_From_Fetch()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: true, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (_, _, _, ct) =>
                    Task.FromException<bool>(new OperationCanceledException(ct)),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.EnsureAllSectionsForPublicationAsync(TestPubCode, LangCodeSections));

            Assert.Single(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_ReturnsFalse_When_Publication_Exists_In_Different_Language()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedPublicationWithSectionsAsync(seed, secondSectionListed: false, secondSectionStored: false);
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (_, _, _, _) => throw new InvalidOperationException("unexpected fetch"),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.EnsureAllSectionsForPublicationAsync(TestPubCode, "ZZ"));
            Assert.Empty(stub.FetchCalls);
        }
    }

    [Fact]
    public async Task Ensure_Uses_Canonical_PublicationCode_For_Gap_With_Lowercase_Input()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            var canonicalPub = AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes;
            await using (var seed = new MediaDbContext(opts))
            {
                var language = new Language
                {
                    LanguageCode = "HV",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var category = new Category { CategoryCode = "CanonSecCat" };

                seed.Languages.Add(language);
                seed.Categories.Add(category);
                await seed.SaveChangesAsync();

                var publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = canonicalPub,
                    Category = category,
                    Language = language,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                };
                seed.PublicationLanguages.Add(publicationLanguage);
                await seed.SaveChangesAsync();

                void AddSectionLanguage(string sectionCode)
                {
                    seed.SectionLanguages.Add(new SectionLanguage
                    {
                        PublicationCode = canonicalPub,
                        SectionCode = sectionCode,
                        LanguageId = language.Id,
                        Language = language,
                        PublicationLanguageId = publicationLanguage.Id,
                        PublicationLanguage = publicationLanguage,
                    });
                }

                AddSectionLanguage("dram-sec-1");
                AddSectionLanguage("dram-sec-2");
                await seed.SaveChangesAsync();

                var biblePublication = new BiblePublication
                {
                    Name = "Video drama canon",
                    PublicationCode = canonicalPub,
                    LanguageId = language.Id,
                    Language = language,
                    IsVideo = true,
                    IsMusic = false,
                };

                seed.BiblePublicationSections.Add(new BiblePublicationSection
                {
                    Name = "One",
                    SectionCode = "dram-sec-1",
                    BiblePublication = biblePublication,
                });

                seed.BiblePublications.Add(biblePublication);
                await seed.SaveChangesAsync();
            }

            var unused = new UnusedLanguageServices();
            var capturedCodes = new List<string>();

            Task<bool> Record(string pub, string lc, IFetchProgress? prog, CancellationToken ct)
            {
                capturedCodes.Add(pub);
                return Task.FromResult(true);
            }

            var stub = new StubFetchSections(unused) { FetchHandler = Record };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsureAllSectionsForPublicationAsync("vodmoviesbibletimes", "hv"));

            Assert.Single(capturedCodes);
            Assert.Equal("vodmoviesbibletimes", capturedCodes[0], StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task Ensure_NoFetch_When_SectionLanguages_Order_Differs_Only_ByCase_From_StoredRows()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

            await using (var seed = new MediaDbContext(opts))
            {
                var language = new Language
                {
                    LanguageCode = "QX",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var category = new Category { CategoryCode = "CaseFoldSecEns" };

                seed.Languages.Add(language);
                seed.Categories.Add(category);
                await seed.SaveChangesAsync();

                const string pubFold = "sec-fold-pub-x";
                var publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = pubFold,
                    Category = category,
                    Language = language,
                    IsMusic = false,
                    CatalogType = CatalogType.Flat,
                };
                seed.PublicationLanguages.Add(publicationLanguage);
                await seed.SaveChangesAsync();

                seed.SectionLanguages.Add(new SectionLanguage
                {
                    PublicationCode = pubFold,
                    SectionCode = "Aa-Bb",
                    LanguageId = language.Id,
                    Language = language,
                    PublicationLanguageId = publicationLanguage.Id,
                    PublicationLanguage = publicationLanguage,
                });
                await seed.SaveChangesAsync();

                var biblePublication = new BiblePublication
                {
                    Name = "Case fold sections",
                    PublicationCode = pubFold,
                    LanguageId = language.Id,
                    Language = language,
                    IsVideo = false,
                    IsMusic = false,
                };

                seed.BiblePublicationSections.Add(new BiblePublicationSection
                {
                    Name = "Folded already",
                    SectionCode = "aa-bb",
                    BiblePublication = biblePublication,
                });

                seed.BiblePublications.Add(biblePublication);
                await seed.SaveChangesAsync();
            }

            var unused = new UnusedLanguageServices();
            var stub = new StubFetchSections(unused)
            {
                FetchHandler = (_, _, _, _) =>
                    Task.FromException<bool>(new InvalidOperationException("unexpected fetch")),
            };

            var sut = new PublicationEnsurerAllSectionsEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsureAllSectionsForPublicationAsync("sec-fold-pub-x", "qx"));
            Assert.Empty(stub.FetchCalls);
        }
    }
}