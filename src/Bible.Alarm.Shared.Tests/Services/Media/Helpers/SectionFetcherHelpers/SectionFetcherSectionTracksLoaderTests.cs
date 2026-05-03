#nullable enable

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionFetcherSectionTracksLoaderTests
{
    private sealed class JsonHandler(string body) : HttpMessageHandler
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

    private sealed class FailIfInvokedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Fail("HTTP client should not be used when tracks already exist.");
            throw new UnreachableException();
        }
    }

    private sealed class AlwaysFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static async Task<(SqliteConnection Connection, MediaDbContext Db, Category BibleCat, Language Lang)>
        CreateDbAsync()
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

        return (connection, db, bibleCat, lang);
    }

    private static BiblePublication CreateBiblePublication(Category bibleCat, Language lang, string sectionCode = "40") =>
        new()
        {
            Name = "NWT",
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            Language = lang,
            LanguageId = lang.Id,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
            ],
            Sections =
            [
                new BiblePublicationSection { Name = "Matthew", SectionCode = sectionCode, Tracks = [] }
            ],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
            CatalogType = CatalogType.Sectioned
        };

    private static FetchSectionTracksRequest BuildRequest(
        MediaDbContext db,
        BiblePublication publication,
        BiblePublicationSection section,
        HttpMessageHandler handler,
        bool replaceExisting = false,
        string normalizedLanguageCode = "E") =>
        new()
        {
            Db = db,
            NormalizedPublicationCode = publication.PublicationCode,
            NormalizedSectionCode = section.SectionCode,
            NormalizedLanguageCode = normalizedLanguageCode,
            PublicationCodeForDb = publication.PublicationCode,
            Publication = publication,
            Section = section,
            CancellationToken = CancellationToken.None,
            ReplaceExisting = replaceExisting
        };

    private static SectionFetcherSectionTracksLoader CreateLoader(HttpMessageHandler handler) =>
        new(new HttpClient(handler), TestLogging.CreateLogger());

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsTrue_SkipsHttp_When_Tracks_Exist_And_Not_Replace()
    {
        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            section.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "1",
                Title = "Existing",
                Publication = pub,
                BiblePublicationId = pub.Id,
                Section = section,
                BiblePublicationSectionId = section.Id,
                TrackUrl = new TrackUrl { Url = "https://existing.example/a.mp3" }
            });
            await db.SaveChangesAsync();

            using var handler = new FailIfInvokedHandler();
            var sut = CreateLoader(handler);
            var req = BuildRequest(db, pub, section, handler);

            Assert.True(await sut.FetchSectionTracksAsync(req));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_Http_Returns_No_Body()
    {
        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new AlwaysFailHandler();
            var sut = CreateLoader(handler);

            Assert.False(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_Json_Has_No_Files_Object()
    {
        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler("{\"pubName\":\"x\"}");
            var sut = CreateLoader(handler);

            Assert.False(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_Format_Array_Is_Empty()
    {
        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler("{\"files\":{\"E\":{\"MP3\":[]}},\"pubName\":\"NWT\"}");
            var sut = CreateLoader(handler);

            Assert.False(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_Language_Or_Format_Missing_In_Files()
    {
        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler("{\"files\":{\"ZZ\":{\"MP3\":[]}},\"pubName\":\"NWT\"}");
            var sut = CreateLoader(handler);

            Assert.False(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_Bible_Persists_Tracks_And_Applies_Chapter_Title_Split()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"title\":\"Genesis - Chapter 5\",\"track\":1}]}},\"pubName\":\"NWT\"}";

        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler(json);
            var sut = CreateLoader(handler);

            Assert.True(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));

            await db.Entry(section).Collection(s => s.Tracks).LoadAsync();
            Assert.Single(section.Tracks);
            Assert.Equal("1", section.Tracks[0].TrackCode);
            Assert.Contains("Chapter 5", section.Tracks[0].Title, StringComparison.Ordinal);
            Assert.Contains("cdn.example", section.Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReplaceExisting_Removes_Old_Tracks_And_URLs()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/new.mp3\"},\"title\":\"Matt\"}]}},\"pubName\":\"NWT\"}";

        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            section.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "1",
                Title = "Old",
                Publication = pub,
                BiblePublicationId = pub.Id,
                Section = section,
                BiblePublicationSectionId = section.Id,
                TrackUrl = new TrackUrl { Url = "https://old.example/o.mp3" }
            });
            await db.SaveChangesAsync();

            using var handler = new JsonHandler(json);
            var sut = CreateLoader(handler);
            var req = BuildRequest(db, pub, section, handler, replaceExisting: true);

            Assert.True(await sut.FetchSectionTracksAsync(req));

            var tracks = await db.BiblePublicationTracks
                .Include(t => t.TrackUrl)
                .Where(t => t.BiblePublicationSectionId == section.Id)
                .ToListAsync();
            Assert.Single(tracks);
            Assert.Contains("new.mp3", tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_Magazine_IssueSectioned_Updates_Section_Name_And_Persists_Track()
    {
        var year = Math.Min(DateTime.UtcNow.Year, MagazineHelper.MagazineEndYear - 1);
        var pubCode = $"w{year}";
        var sectionCode = $"{year}0101-wp";

        const string jsonTemplate =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://mag.example/a.mp3\"},\"track\":3,\"title\":{\"text\":\"Article\"}}]}},\"pubName\":\"Watchtower&nbsp;Study\",\"formattedDate\":\"January&nbsp;2026\"}";
        var json = jsonTemplate.Replace("2026", year.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        var (connection, db, _, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var wtCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine };
            db.Categories.Add(wtCat);
            await db.SaveChangesAsync();

            var pub = new BiblePublication
            {
                Name = "WT",
                PublicationCode = pubCode,
                Language = lang,
                LanguageId = lang.Id,
                BiblePublicationCategories =
                [
                    new BiblePublicationCategory { Category = wtCat, CategoryId = wtCat.Id }
                ],
                Sections =
                [
                    new BiblePublicationSection { Name = "Placeholder", SectionCode = sectionCode, Tracks = [] }
                ],
                Tracks = [],
                IsVideo = false,
                IsMusic = false,
                CatalogType = CatalogType.IssueSectioned
            };
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler(json);
            var sut = CreateLoader(handler);

            Assert.True(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));

            await db.Entry(section).ReloadAsync();
            Assert.False(string.IsNullOrWhiteSpace(section.Name));
            Assert.Contains("Watchtower", section.Name, StringComparison.OrdinalIgnoreCase);

            await db.Entry(section).Collection(s => s.Tracks).LoadAsync();
            Assert.Single(section.Tracks);
            Assert.Equal("3", section.Tracks[0].TrackCode);
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_Video_Drama_Uses_Mp4_And_Section_Code_As_Track_Code()
    {
        var dramaCat = AppConstants.Media.BiblePublicationCategoryDramas;
        var sectionPubCode = "dramasec1";

        var json =
            "{\"files\":{\"E\":{\"MP4\":[{\"file\":{\"url\":\"https://vid.example/x.mp4\"},\"title\":\"Scene\"}]}},\"pubName\":\"Video Drama Title\"}";

        var (connection, db, _, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var cat = new Category { CategoryCode = dramaCat };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            var pub = new BiblePublication
            {
                Name = "Drama",
                PublicationCode = "custom-drama-pub",
                Language = lang,
                LanguageId = lang.Id,
                BiblePublicationCategories =
                [
                    new BiblePublicationCategory { Category = cat, CategoryId = cat.Id }
                ],
                Sections =
                [
                    new BiblePublicationSection { Name = "Ep1", SectionCode = sectionPubCode, Tracks = [] }
                ],
                Tracks = [],
                IsVideo = true,
                IsMusic = false,
                CatalogType = CatalogType.MediatorSectioned
            };
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler(json);
            var sut = CreateLoader(handler);

            Assert.True(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));

            await db.Entry(section).Collection(s => s.Tracks).LoadAsync();
            Assert.Single(section.Tracks);
            Assert.Equal(sectionPubCode, section.Tracks[0].TrackCode);
            Assert.Contains(".mp4", section.Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_Music_NonVideo_Uses_Api_Track_Number_When_Present()
    {
        var json =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://music.example/s.mp3\"},\"track\":42,\"title\":\"Song\"}]}},\"pubName\":\"Song Book\"}";

        var (connection, db, _, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            db.Categories.Add(musicCat);
            await db.SaveChangesAsync();

            var pub = new BiblePublication
            {
                Name = "Vocal",
                PublicationCode = "sjj-music-pub",
                Language = lang,
                LanguageId = lang.Id,
                BiblePublicationCategories =
                [
                    new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id }
                ],
                Sections =
                [
                    new BiblePublicationSection { Name = "Disc", SectionCode = "iam-1", Tracks = [] }
                ],
                Tracks = [],
                IsVideo = false,
                IsMusic = true,
                CatalogType = CatalogType.Sectioned
            };
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            using var handler = new JsonHandler(json);
            var sut = CreateLoader(handler);

            Assert.True(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));

            await db.Entry(section).Collection(s => s.Tracks).LoadAsync();
            Assert.Single(section.Tracks);
            Assert.Equal("42", section.Tracks[0].TrackCode);
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_Attaches_Detached_Section_Before_Load()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"title\":\"Matt\"}]}},\"pubName\":\"NWT\"}";

        var (connection, db, bibleCat, lang) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            var pub = CreateBiblePublication(bibleCat, lang);
            db.BiblePublications.Add(pub);
            await db.SaveChangesAsync();

            var section = pub.Sections[0];
            db.Entry(section).State = EntityState.Detached;

            using var handler = new JsonHandler(json);
            var sut = CreateLoader(handler);

            Assert.True(await sut.FetchSectionTracksAsync(BuildRequest(db, pub, section, handler)));

            await db.Entry(section).Collection(s => s.Tracks).LoadAsync();
            Assert.Single(section.Tracks);
        }
    }
}
