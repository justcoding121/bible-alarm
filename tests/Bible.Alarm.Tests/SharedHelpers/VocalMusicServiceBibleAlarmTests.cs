#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class VocalMusicServiceBibleAlarmTests
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

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("simulated scope failure");
    }

    [Fact]
    public async Task GetDistinctLanguagesAsync_Includes_Language_When_Vocal_BiblePublication_Exists()
    {
        const string pubCode = "vms-dist-pos";
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var lang = new Language
                {
                    LanguageCode = "VMZ",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);

                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                db.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = pubCode,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true,
                });

                db.BiblePublications.Add(new BiblePublication
                {
                    Name = "Vocal seeded",
                    PublicationCode = pubCode,
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
            var map = await sut.GetDistinctLanguagesAsync();

            Assert.True(map.TryGetValue("VMZ", out var row));
            Assert.Equal("VMZ", row.LanguageCode, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetDistinctLanguagesAsync_Drops_Language_When_Only_Pl_Row_Has_No_Vocal_Shell()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var lang = new Language
                {
                    LanguageCode = "VMY",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);

                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                db.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "pl-only-shell",
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true,
                });
                await db.SaveChangesAsync();
            }

            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());

            var map = await sut.GetDistinctLanguagesAsync();
            Assert.DoesNotContain(
                map.Keys,
                c => string.Equals(c, "VMY", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task GetTracksByLanguageAndCodeAsync_Omits_Section_Bound_Tracks()
    {
        const string pubCode = "vms-sec-flat";
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
                    Name = "Mixed tracks",
                    PublicationCode = pubCode,
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
                };
                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();

                var section = new BiblePublicationSection
                {
                    Name = "Blk",
                    SectionCode = "blk",
                    BiblePublicationId = publication.Id,
                    BiblePublication = publication,
                };
                db.BiblePublicationSections.Add(section);
                await db.SaveChangesAsync();

                publication.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "in-section",
                    Title = "S",
                    Publication = publication,
                    BiblePublicationId = publication.Id,
                    Section = section,
                    BiblePublicationSectionId = section.Id,
                });
                publication.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "flat-only",
                    Title = "F",
                    Publication = publication,
                    BiblePublicationId = publication.Id,
                    BiblePublicationSectionId = null,
                });
                await db.SaveChangesAsync();
            }

            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var dict = await sut.GetTracksByLanguageAndCodeAsync(AppConstants.Media.DefaultLanguageCode, pubCode);

            Assert.Single(dict);
            Assert.Equal("flat-only", dict[0].TrackCode);
        }
    }

    [Fact]
    public async Task GetTracksByLanguageAndCodeAsync_ReturnsEmpty_When_Publication_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            Assert.Empty(await sut.GetTracksByLanguageAndCodeAsync("E", "no-such-vocal-pub-wave"));
        }
    }

    [Fact]
    public async Task GetByLanguageCodeAsync_Keeps_First_When_Duplicate_PublicationCode_Rows()
    {
        const string dupCode = "vms-dup-code";
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var lang = new Language
                {
                    LanguageCode = "VQU",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);

                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                void AddRow(string title) =>
                    db.BiblePublications.Add(new BiblePublication
                    {
                        Name = title,
                        PublicationCode = dupCode,
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

                AddRow("FirstDup");
                AddRow("SecondDup");
                await db.SaveChangesAsync();
            }

            using var sut = new VocalMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var map = await sut.GetByLanguageCodeAsync("VQU");

            Assert.Single(map);
            Assert.Equal("FirstDup", map[dupCode].Publication.Name);
        }
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

        using (var bootstrap = new MediaDbContext(opts))
        {
            bootstrap.Database.EnsureCreated();
        }

        try
        {
            var sut = new VocalMusicService(new MediaTestScopeFactory(opts), TestLogging.CreateLogger());
            sut.Dispose();
            sut.Dispose();
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeAsync_Wraps_Scope_Failure()
    {
        var sut = new VocalMusicService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetByLanguageAndCodeAsync("E", "x"));

        Assert.Contains("Error getting vocal music", ex.Message, StringComparison.Ordinal);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }
}
