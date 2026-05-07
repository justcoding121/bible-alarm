#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionFetcherTests
{
    private const string MinimalBiblePubMediaJson =
        "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"track\":1,\"title\":\"Matt\"}]}},\"pubName\":\"NWT\"}";

    private const string MinimalBiblePubMediaJsonWithMisleadingParentPubName =
        "{\"parentPubName\":\"The Good News According to Jesus\",\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"track\":1,\"title\":\"Matt\"}]}},\"pubName\":\"NWT\"}";

    private const string MinimalMagazineIssuePubMediaJson =
        "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/wt.mp3\"},\"track\":1,\"title\":\"Study\"}]}},\"pubName\":\"Watchtower\",\"formattedDate\":\"Feb\"}";

    private const string MinimalVideoMediatorPubMediaJson =
        "{\"files\":{\"E\":{\"MP4\":[{\"file\":\"https://v/a.mp4\",\"title\":\"Scene\"}]}}}";

    private sealed class JsonResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(ex);
    }

    private sealed class RecordingProgress : IFetchProgress
    {
        private readonly CancellationToken token;

        public RecordingProgress(CancellationToken token)
        {
            this.token = token;
        }

        public List<double> Values { get; } = [];

        public CancellationToken CancellationToken => token;

        public void UpdateProgress(double progress) => Values.Add(progress);

        public void UpdateProgressText(string text)
        {
        }

        public void SetIsVisible(bool isVisible)
        {
        }
    }

    private static async Task<(SqliteConnection Connection, MediaDbContext Db, Category BibleCat, Language Lang, BiblePublication EnglishStub)>
        CreatePreparedDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        var db = new MediaDbContext(options);
        var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
        var lang = new Language { LanguageCode = AppConstants.Media.DefaultLanguageCode, Direction = AppConstants.Media.TextDirectionLeftToRight };
        db.Categories.Add(bibleCat);
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var englishStub = new BiblePublication
        {
            Name = "NWT",
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
            ],
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false
        };

        return (connection, db, bibleCat, lang, englishStub);
    }

    private static async Task<(SqliteConnection Connection, MediaDbContext Db, Category WatchtowerCat, Language Lang, BiblePublication EnglishStub)>
        CreatePreparedWatchtowerMagazineDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        var db = new MediaDbContext(options);
        var watchtowerCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine };
        var lang = new Language { LanguageCode = AppConstants.Media.DefaultLanguageCode, Direction = AppConstants.Media.TextDirectionLeftToRight };
        db.Categories.Add(watchtowerCat);
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var englishStub = new BiblePublication
        {
            Name = "Watchtower Stub",
            PublicationCode = "w2020",
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { Category = watchtowerCat, CategoryId = watchtowerCat.Id }
            ],
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false
        };

        return (connection, db, watchtowerCat, lang, englishStub);
    }

    private static async Task<(SqliteConnection Connection, MediaDbContext Db, Category DramaCat, Language Lang, BiblePublication EnglishStub)>
        CreatePreparedVideoDramaDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        var db = new MediaDbContext(options);
        var dramaCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryDramas };
        var lang = new Language { LanguageCode = AppConstants.Media.DefaultLanguageCode, Direction = AppConstants.Media.TextDirectionLeftToRight };
        db.Categories.Add(dramaCat);
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var englishStub = new BiblePublication
        {
            Name = "Drama Stub",
            PublicationCode = AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { Category = dramaCat, CategoryId = dramaCat.Id }
            ],
            Sections = [],
            Tracks = [],
            IsVideo = true,
            IsMusic = false
        };

        return (connection, db, dramaCat, lang, englishStub);
    }

    private static FetchPublicationSectionsRequest BuildRequest(
        MediaDbContext db,
        BiblePublication english,
        IReadOnlyList<string> sectionCodes,
        CancellationToken cancellationToken = default,
        IFetchProgress? progress = null) =>
        new()
        {
            Db = db,
            NormalizedPublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
            PublicationCodeForDb = AppConstants.Media.BiblePublicationCodeNwt,
            EnglishPublication = english,
            SectionCodes = sectionCodes,
            CancellationToken = cancellationToken,
            Progress = progress
        };

    private static FetchPublicationSectionsRequest BuildWatchtowerMagazineRequest(
        MediaDbContext db,
        BiblePublication english,
        IReadOnlyList<string> sectionCodes,
        CancellationToken cancellationToken = default,
        IFetchProgress? progress = null) =>
        new()
        {
            Db = db,
            NormalizedPublicationCode = "w2020",
            NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
            PublicationCodeForDb = "w2020",
            EnglishPublication = english,
            SectionCodes = sectionCodes,
            CancellationToken = cancellationToken,
            Progress = progress
        };

    private static FetchPublicationSectionsRequest BuildVideoDramaRequest(
        MediaDbContext db,
        BiblePublication english,
        IReadOnlyList<string> sectionCodes,
        CancellationToken cancellationToken = default,
        IFetchProgress? progress = null) =>
        new()
        {
            Db = db,
            NormalizedPublicationCode = AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
            NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
            PublicationCodeForDb = AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
            EnglishPublication = english,
            SectionCodes = sectionCodes,
            CancellationToken = cancellationToken,
            Progress = progress
        };

    [Fact]
    public void Constructor_Throws_When_HttpClient_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new SectionFetcher(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        using var client = new HttpClient(new JsonResponseHandler("{}"));
        Assert.Throws<ArgumentNullException>(() => new SectionFetcher(client, null!));
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_Language_Missing()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());
            var req = BuildRequest(db, englishStub, ["mat"], progress: new RecordingProgress(CancellationToken.None));
            req = new FetchPublicationSectionsRequest
            {
                Db = req.Db,
                NormalizedPublicationCode = req.NormalizedPublicationCode,
                NormalizedLanguageCode = "ZZ",
                PublicationCodeForDb = req.PublicationCodeForDb,
                EnglishPublication = req.EnglishPublication,
                SectionCodes = req.SectionCodes,
                CancellationToken = req.CancellationToken,
                Progress = req.Progress
            };

            Assert.False(await sut.FetchPublicationSectionsAsync(req));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_English_Publication_Has_No_Primary_Category()
    {
        var (connection, db, _, _, _) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());
            var englishNoCategory = new BiblePublication
            {
                Name = "NWT",
                PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                BiblePublicationCategories = [],
                Sections = [],
                Tracks = [],
                IsVideo = false,
                IsMusic = false
            };

            Assert.False(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishNoCategory, ["mat"])));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_No_Categories_Match_Publication_In_Db()
    {
        var (connection, db, bibleCat, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            db.Categories.Remove(bibleCat);
            await db.SaveChangesAsync();
            await db.Categories.AddAsync(new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic });
            await db.SaveChangesAsync();

            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.False(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_When_All_Sections_Exist_Syncs_Categories_And_Completes_Progress()
    {
        var (connection, db, bibleCat, lang, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            var existing = new BiblePublication
            {
                PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                Name = "NWT",
                Language = lang,
                LanguageId = lang.Id,
                Sections =
                [
                    new BiblePublicationSection { Name = "Matthew", SectionCode = "mat", Tracks = [] }
                ],
                Tracks = [],
                IsVideo = false,
                IsMusic = false,
                BiblePublicationCategories = []
            };
            db.BiblePublications.Add(existing);
            await db.SaveChangesAsync();

            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());
            var progress = new RecordingProgress(CancellationToken.None);

            Assert.True(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"], progress: progress)));

            Assert.Contains(1.0, progress.Values);
            await db.Entry(existing).Collection(x => x.BiblePublicationCategories).LoadAsync();
            Assert.Contains(existing.BiblePublicationCategories, bpc => bpc.CategoryId == bibleCat.Id);
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Removes_Empty_Publication_When_Api_Returns_No_Body()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var inner = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            using var client = new HttpClient(inner);
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.False(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_NonHttpFailure_Per_Section_Without_Persisting_Section()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new ThrowingHandler(new ArithmeticException("simulated")));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.False(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Persists_Section_And_Tracks_When_Api_Returns_Valid_Bible_Json()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.True(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));

            var bp = await db.BiblePublications.Include(b => b.Sections).ThenInclude(s => s.Tracks).ThenInclude(t => t.TrackUrl!).SingleAsync();
            Assert.Single(bp.Sections);
            Assert.Single(bp.Sections[0].Tracks);
            Assert.Contains("cdn.example", bp.Sections[0].Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Uses_Progress_CancellationToken()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var progress = new RecordingProgress(cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"], progress: progress)));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Uses_Request_CancellationToken_When_Progress_Absent()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"], cancellationToken: cts.Token)));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Persists_Magazine_Issue_Section_With_Year_Localized_As_Name()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedWatchtowerMagazineDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalMagazineIssuePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.True(await sut.FetchPublicationSectionsAsync(
                BuildWatchtowerMagazineRequest(db, englishStub, ["20090201-wp"])));

            var bp = await db.BiblePublications
                .Include(b => b.Sections)
                .ThenInclude(s => s.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync();
            Assert.Equal("2020", bp.Name);
            Assert.Single(bp.Sections);
            Assert.Contains("Feb", bp.Sections[0].Name, StringComparison.OrdinalIgnoreCase);
            Assert.Single(bp.Sections[0].Tracks);
            Assert.Contains("wt.mp3", bp.Sections[0].Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Persists_Video_Mediator_Section_Using_Mp4_Files_Element()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedVideoDramaDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalVideoMediatorPubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.True(await sut.FetchPublicationSectionsAsync(
                BuildVideoDramaRequest(db, englishStub, ["m-1"])));

            var bp = await db.BiblePublications
                .Include(b => b.Sections)
                .ThenInclude(s => s.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync();
            Assert.Single(bp.Sections);
            Assert.Equal("m-1", bp.Sections[0].SectionCode);
            Assert.Single(bp.Sections[0].Tracks);
            Assert.Contains("v/a.mp4", bp.Sections[0].Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Propagates_TimeoutException_From_Http()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new ThrowingHandler(new TimeoutException("simulated stall")));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            await Assert.ThrowsAsync<TimeoutException>(() =>
                sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_Response_Is_Invalid_Json()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler("{"));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.False(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Ignores_Misleading_Video_ParentPubName_For_Bible()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJsonWithMisleadingParentPubName));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.True(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));

            var bp = await db.BiblePublications.SingleAsync();
            Assert.Equal("NWT", bp.Name);
            Assert.DoesNotContain("Good News According to Jesus", bp.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Reports_Fractional_Progress_For_Multiple_Sections()
    {
        var (connection, db, _, _, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());
            var progress = new RecordingProgress(CancellationToken.None);

            Assert.True(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat", "mrk"], progress: progress)));

            Assert.Contains(0.5, progress.Values);
            Assert.Contains(1.0, progress.Values);
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_Backfills_Null_CatalogType_On_Existing_Publication()
    {
        var (connection, db, bibleCat, lang, englishStub) = await CreatePreparedDbAsync();
        await using (connection)
        await using (db)
        {
            db.BiblePublications.Add(new BiblePublication
            {
                PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                Name = "NWT Seed",
                Language = lang,
                LanguageId = lang.Id,
                CatalogType = null,
                Sections = [],
                Tracks = [],
                IsVideo = false,
                IsMusic = false,
                BiblePublicationCategories =
                [
                    new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                ],
            });
            await db.SaveChangesAsync();

            using var client = new HttpClient(new JsonResponseHandler(MinimalBiblePubMediaJson));
            var sut = new SectionFetcher(client, TestLogging.CreateLogger());

            Assert.True(await sut.FetchPublicationSectionsAsync(BuildRequest(db, englishStub, ["mat"])));

            var bp = await db.BiblePublications.SingleAsync();
            Assert.NotNull(bp.CatalogType);
        }
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
