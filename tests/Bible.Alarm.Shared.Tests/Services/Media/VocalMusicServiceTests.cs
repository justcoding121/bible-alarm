#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class VocalMusicServiceTests
{
    private const string TestPublicationCode = "vocal-pub-tests";

    private static async Task<(SqliteConnection Connection, DbContextOptions<MediaDbContext> Options)> CreateConnectionAndOptionsAsync()
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

        return (connection, options);
    }

    [Fact]
    public void Constructor_ThrowsWhenScopeFactoryNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new VocalMusicService(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public async Task Constructor_ThrowsWhenLoggerNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new VocalMusicService(new MediaTestScopeFactory(options), null!));
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeAsync_ReturnsNull_When_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            Assert.Null(await sut.GetByLanguageAndCodeAsync(AppConstants.Media.DefaultLanguageCode, TestPublicationCode));
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeAsync_Returns_Publication_When_Seeded()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var lang = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);

                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                db.BiblePublications.Add(new BiblePublication
                {
                    Name = "Songs test",
                    PublicationCode = TestPublicationCode,
                    LanguageId = lang.Id,
                    Language = lang,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await db.SaveChangesAsync();
            }

            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var vm = await sut.GetByLanguageAndCodeAsync(AppConstants.Media.DefaultLanguageCode, TestPublicationCode);

            Assert.NotNull(vm);
            Assert.Equal(TestPublicationCode, vm!.Publication.PublicationCode, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(
                AppConstants.Media.DefaultLanguageCode,
                vm.Publication.Language?.LanguageCode,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetTracksByLanguageAndCodeAsync_Returns_Flat_Tracks_Only()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var lang = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);

                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var publication = new BiblePublication
                {
                    Name = "Songs tracks",
                    PublicationCode = TestPublicationCode,
                    LanguageId = lang.Id,
                    Language = lang,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks =
                    [
                        new BiblePublicationTrack
                        {
                            TrackCode = "7",
                            Title = "Seven",
                            Publication = default!,
                            BiblePublicationSectionId = null,
                        },
                        new BiblePublicationTrack
                        {
                            TrackCode = "8",
                            Title = "Eight",
                            Publication = default!,
                            BiblePublicationSectionId = null,
                        },
                    ],
                };
                foreach (var t in publication.Tracks)
                {
                    t.Publication = publication;
                }

                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();
            }

            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var dict = await sut.GetTracksByLanguageAndCodeAsync(AppConstants.Media.DefaultLanguageCode, TestPublicationCode);

            Assert.Equal(2, dict.Count);
            var codes = dict.Values.Select(v => v.TrackCode!).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "7", "8" }, codes);
        }
    }

    [Fact]
    public async Task GetByLanguageCodeAsync_KeyedBy_PublicationCode()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var lang = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);

                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                void AddVocalPublication(string code) =>
                    db.BiblePublications.Add(new BiblePublication
                    {
                        Name = $"Vocal {code}",
                        PublicationCode = code,
                        LanguageId = lang.Id,
                        Language = lang,
                        IsMusic = true,
                        IsVideo = false,
                        BiblePublicationCategories =
                        [
                            new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                        ],
                        Sections = [],
                        Tracks = [],
                    });

                AddVocalPublication($"{TestPublicationCode}a");
                AddVocalPublication($"{TestPublicationCode}b");
                await db.SaveChangesAsync();
            }

            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var map = await sut.GetByLanguageCodeAsync(AppConstants.Media.DefaultLanguageCode);

            Assert.Equal(2, map.Count);
            Assert.True(map.TryGetValue($"{TestPublicationCode}a", out VocalMusic? a));
            Assert.True(map.TryGetValue($"{TestPublicationCode}b", out VocalMusic? b));
            Assert.NotNull(a!.Publication.Language);
        }
    }
}
