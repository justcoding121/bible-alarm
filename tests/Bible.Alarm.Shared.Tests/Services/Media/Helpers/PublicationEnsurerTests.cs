#nullable enable

using System.Net;
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

public sealed class PublicationEnsurerTests
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

    private sealed class StubLanguageContentService : ILanguageContentService
    {
        public List<(string Pub, string Lang)> FetchTracksCalls { get; } = [];
        public List<(string Pub, string Lang, string? Section)> FetchSectionTracksCalls { get; } = [];
        public List<(string Pub, string Lang)> FetchSectionsCalls { get; } = [];
        public List<(string Pub, string Lang, string Section)> FetchFirstSectionOnlyCalls { get; } = [];

        public Func<string, string, CancellationToken, Task<bool>> FetchPublicationTracksAsyncHandler { get; set; } =
            (_, _, _) => Task.FromResult(false);

        public Func<string, string, string, bool, CancellationToken, Task<bool>> FetchSectionTracksAsyncHandler { get; set; } =
            (_, _, _, _, _) => Task.FromResult(false);

        public Func<string, string, IFetchProgress?, CancellationToken, Task<bool>> FetchPublicationSectionsAsyncHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(false);

        public Func<string, string, string, CancellationToken, Task<bool>> FetchFirstSectionOnlyAsyncHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(false);

        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken)
        {
            FetchTracksCalls.Add((publicationCode, languageCode));
            return FetchPublicationTracksAsyncHandler(publicationCode, languageCode, cancellationToken);
        }

        public Task<bool> FetchPublicationSectionsAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken)
        {
            FetchSectionsCalls.Add((publicationCode, languageCode));
            return FetchPublicationSectionsAsyncHandler(publicationCode, languageCode, progress, cancellationToken);
        }

        public Task<bool> FetchSectionTracksAsync(
            string publicationCode,
            string sectionCode,
            string languageCode,
            bool replaceExistingTracksFromApi,
            CancellationToken cancellationToken)
        {
            FetchSectionTracksCalls.Add((publicationCode, languageCode, sectionCode));
            return FetchSectionTracksAsyncHandler(publicationCode, sectionCode, languageCode, replaceExistingTracksFromApi, cancellationToken);
        }

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> EnsurePublicationExistsAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(
            string languageCode,
            string? categoryName,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(
            string publicationCode,
            string languageCode,
            IFetchProgress? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(
            string publicationCode,
            string firstSectionCode,
            string languageCode,
            CancellationToken cancellationToken)
        {
            FetchFirstSectionOnlyCalls.Add((publicationCode, languageCode, firstSectionCode));
            return FetchFirstSectionOnlyAsyncHandler(publicationCode, firstSectionCode, languageCode, cancellationToken);
        }
    }

    private sealed class CaptureProgress : IFetchProgress
    {
        public List<double> Values { get; } = [];
        public CancellationToken CancellationToken => CancellationToken.None;

        public void UpdateProgress(double progress) => Values.Add(progress);

        public void UpdateProgressText(string text) { }

        public void SetIsVisible(bool isVisible) { }
    }

    private static async Task SeedCategoryLanguageAsync(MediaDbContext db, string langCode = "M")
    {
        var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
        db.Categories.Add(cat);
        db.Languages.Add(new Language
        {
            LanguageCode = langCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Null()
    {
        var stub = new StubLanguageContentService();
        Assert.Throws<ArgumentNullException>(() =>
            new PublicationEnsurer(null!, TestLogging.CreateLogger(), stub));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        var (factory, connection) = CreateFactorySync();
        using (connection)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PublicationEnsurer(factory, null!, new StubLanguageContentService()));
        }
    }

    [Fact]
    public void Constructor_Throws_When_LanguageContentService_Null()
    {
        var (factory, connection) = CreateFactorySync();
        using (connection)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PublicationEnsurer(factory, TestLogging.CreateLogger(), null!));
        }
    }

    private static (MediaTestScopeFactory Factory, SqliteConnection Connection) CreateFactorySync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var init = new MediaDbContext(options))
        {
            init.Database.EnsureCreated();
        }

        return (new MediaTestScopeFactory(options), connection);
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_ReturnsTrue_When_Already_In_Database()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var bibleCat = await seed.Categories.SingleAsync();
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "nwt",
                    Name = "NWT",
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Sectioned,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsurePublicationExistsAsync("nwt", "M"));
            Assert.Empty(stub.FetchTracksCalls);
            Assert.Empty(stub.FetchSectionTracksCalls);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Backfills_CatalogType_When_Null()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var bibleCat = await seed.Categories.SingleAsync();
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "nwt",
                    Name = "NWT",
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = null,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsurePublicationExistsAsync("nwt", "M"));

            await using var verify = new MediaDbContext(opts);
            var bp = await verify.BiblePublications.AsNoTracking().SingleAsync();
            Assert.Equal(CatalogType.Sectioned, bp.CatalogType);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_ReturnsFalse_When_Not_In_PublicationLanguages()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.EnsurePublicationExistsAsync("missing-pub", "M"));
            Assert.Empty(stub.FetchTracksCalls);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_ReturnsFalse_When_PublicationLanguage_Has_No_Language_Id()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "iam-null-lang",
                    Category = cat,
                    CategoryId = cat.Id,
                    LanguageId = null,
                    Language = null,
                    CatalogType = CatalogType.Sectioned,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.EnsurePublicationExistsAsync("iam-null-lang", "M"));
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Flat_Calls_FetchPublicationTracks_And_Reports_Progress()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "flat-pub-x",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);
            var progress = new CaptureProgress();

            Assert.True(await sut.EnsurePublicationExistsAsync("flat-pub-x", "M", progress));

            Assert.Single(stub.FetchTracksCalls);
            Assert.Equal(("flat-pub-x", "M"), stub.FetchTracksCalls[0]);
            Assert.Equal([0.0, 1.0], progress.Values);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Sectioned_NewPublication_Fetches_First_Section_Then_Tracks()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                var pl = new PublicationLanguage
                {
                    PublicationCode = "sec-new-pub",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Sectioned,
                    IsMusic = false
                };
                seed.PublicationLanguages.Add(pl);
                await seed.SaveChangesAsync();

                seed.SectionLanguages.Add(new SectionLanguage
                {
                    PublicationCode = "sec-new-pub",
                    SectionCode = "40",
                    Language = lang,
                    PublicationLanguage = pl
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchFirstSectionOnlyAsyncHandler = (_, _, _, _) => Task.FromResult(true),
                FetchSectionTracksAsyncHandler = (_, _, _, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);
            var progress = new CaptureProgress();

            Assert.True(await sut.EnsurePublicationExistsAsync("sec-new-pub", "M", progress));

            Assert.Single(stub.FetchFirstSectionOnlyCalls);
            Assert.Equal(("sec-new-pub", "M", "40"), stub.FetchFirstSectionOnlyCalls[0]);
            Assert.Single(stub.FetchSectionTracksCalls);
            Assert.Contains(0.5, progress.Values);
            Assert.Contains(1.0, progress.Values);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_ReturnsFalse_When_No_SectionLanguages_For_Publication()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "sec-no-sl",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Sectioned,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.EnsurePublicationExistsAsync("sec-no-sl", "M"));
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Swallows_Generic_Exception_Returns_False()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "flat-fail",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => throw new FormatException("simulated")
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.EnsurePublicationExistsAsync("flat-fail", "M"));
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Rethrows_HttpRequestException()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "flat-http",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => throw new HttpRequestException("network")
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.EnsurePublicationExistsAsync("flat-http", "M"));
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Rethrows_SocketException()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "flat-socket",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => throw new SocketException(99)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<SocketException>(() =>
                sut.EnsurePublicationExistsAsync("flat-socket", "M"));
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_ReturnsTrue_For_Default_English_Without_Fetch()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.FetchFirstPublicationForLanguageAsync(AppConstants.Media.DefaultLanguageCode.ToLowerInvariant()));

            Assert.Empty(stub.FetchTracksCalls);
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_ReturnsFalse_When_No_PublicationLanguages_For_Language()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.FetchFirstPublicationForLanguageAsync("M"));
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_Flat_Fetches_Tracks_For_First_Ordered_Candidate()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                foreach (var code in new[] { "zebra-flat", "alpha-flat" })
                {
                    seed.PublicationLanguages.Add(new PublicationLanguage
                    {
                        PublicationCode = code,
                        Category = cat,
                        CategoryId = cat.Id,
                        Language = lang,
                        LanguageId = lang.Id,
                        CatalogType = CatalogType.Flat,
                        IsMusic = false
                    });
                }

                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (pub, _, _) =>
                    Task.FromResult(string.Equals(pub, "alpha-flat", StringComparison.OrdinalIgnoreCase))
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.FetchFirstPublicationForLanguageAsync("M"));

            Assert.Equal("alpha-flat", stub.FetchTracksCalls[0].Pub);
            Assert.Single(stub.FetchTracksCalls);
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_Sectioned_Uncataloged_Calls_First_Section_Then_Tracks()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                var pl = new PublicationLanguage
                {
                    PublicationCode = "ffpl-sec-pub",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Sectioned,
                    IsMusic = false
                };
                seed.PublicationLanguages.Add(pl);
                await seed.SaveChangesAsync();

                seed.SectionLanguages.Add(new SectionLanguage
                {
                    PublicationCode = "ffpl-sec-pub",
                    SectionCode = "10",
                    Language = lang,
                    PublicationLanguage = pl
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchFirstSectionOnlyAsyncHandler = (_, _, _, _) => Task.FromResult(true),
                FetchSectionTracksAsyncHandler = (_, _, _, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.FetchFirstPublicationForLanguageAsync("M"));

            Assert.Single(stub.FetchFirstSectionOnlyCalls);
            Assert.Single(stub.FetchSectionTracksCalls);
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_Filters_By_Category_When_Provided()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var bible = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var music = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                seed.Categories.AddRange(bible, music);
                seed.Languages.Add(new Language
                {
                    LanguageCode = "M",
                    Direction = AppConstants.Media.TextDirectionLeftToRight
                });
                await seed.SaveChangesAsync();

                var lang = await seed.Languages.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "music-only",
                    Category = music,
                    CategoryId = music.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.FetchFirstPublicationForLanguageAsync(
                "M",
                AppConstants.Media.BiblePublicationCategoryBible));

            Assert.Empty(stub.FetchTracksCalls);

            Assert.True(await sut.FetchFirstPublicationForLanguageAsync(
                "M",
                AppConstants.Media.BiblePublicationCategoryMusic));

            Assert.Single(stub.FetchTracksCalls);
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_ReturnsFalse_When_All_Candidates_Fail()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "solo-flat",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.FetchFirstPublicationForLanguageAsync("M"));
            Assert.Single(stub.FetchTracksCalls);
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_Swallows_Generic_Exception()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "boom-flat",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => throw new InvalidOperationException("x")
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.False(await sut.FetchFirstPublicationForLanguageAsync("M"));
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_Rethrows_HttpRequestException()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "net-flat",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => throw new HttpRequestException("network")
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.FetchFirstPublicationForLanguageAsync("M"));
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_MediatorSectioned_Calls_FetchPublicationTracks()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "drama-med-z",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.MediatorSectioned,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsurePublicationExistsAsync("drama-med-z", "M"));

            Assert.Single(stub.FetchTracksCalls);
            Assert.Equal(("drama-med-z", "M"), stub.FetchTracksCalls[0]);
            Assert.Empty(stub.FetchSectionTracksCalls);
            Assert.Empty(stub.FetchFirstSectionOnlyCalls);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Looks_Up_Pl_Using_Canonical_Publication_Code()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.MediatorSectioned,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsurePublicationExistsAsync("vodmoviesbibletimes", "M"));

            Assert.Single(stub.FetchTracksCalls);
            Assert.Equal("vodmoviesbibletimes", stub.FetchTracksCalls[0].Pub);
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_MediatorSectioned_Uncataloged_Calls_FetchTracks()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "ffpl-med-only",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.MediatorSectioned,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.FetchFirstPublicationForLanguageAsync("M"));

            Assert.Single(stub.FetchTracksCalls);
            Assert.Equal("ffpl-med-only", stub.FetchTracksCalls[0].Pub);
        }
    }

    [Fact]
    public async Task EnsurePublicationExistsAsync_Rethrows_OperationCanceledException()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "flat-oce",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) =>
                    throw new OperationCanceledException()
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.EnsurePublicationExistsAsync("flat-oce", "M"));
        }
    }

    [Fact]
    public async Task FetchFirstPublicationForLanguageAsync_Skips_Cataloged_Candidates_In_Order()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedCategoryLanguageAsync(seed);
                var lang = await seed.Languages.SingleAsync();
                var cat = await seed.Categories.SingleAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "alpha-skip-flat",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "zebra-flat",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    IsMusic = false
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "alpha-skip-flat",
                    Name = "Has rows",
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Flat,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = cat, CategoryId = cat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var stub = new StubLanguageContentService
            {
                FetchPublicationTracksAsyncHandler = (_, _, _) => Task.FromResult(true)
            };
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.FetchFirstPublicationForLanguageAsync("M"));

            Assert.Single(stub.FetchTracksCalls);
            Assert.Equal("zebra-flat", stub.FetchTracksCalls[0].Pub);
        }
    }

    [Fact]
    public async Task EnsureAllPublicationsForLanguageAsync_ReturnsTrue_For_English()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var stub = new StubLanguageContentService();
            var sut = new PublicationEnsurer(factory, TestLogging.CreateLogger(), stub);

            Assert.True(await sut.EnsureAllPublicationsForLanguageAsync(AppConstants.Media.DefaultLanguageCode));
        }
    }
}