#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionServiceBibleAlarmTests
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

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new BiblePublicationSectionService(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new BiblePublicationSectionService(new MediaTestScopeFactory(
                new DbContextOptionsBuilder<MediaDbContext>().Options), null!));

    [Fact]
    public async Task GetSectionNameAsync_returns_null_when_section_code_missing()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            using var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            Assert.Null(await sut.GetSectionNameAsync("E", "nwt", ""));
            Assert.Null(await sut.GetSectionNameAsync("E", "nwt", "   "));
        }
    }

    [Fact]
    public async Task GetSectionNameAsync_and_GetSectionAsync_resolve_by_keys()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(bibleCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    PublicationCode = "bpss-nwt",
                    Name = "NwT",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                    Sections =
                    [
                        new BiblePublicationSection
                        {
                            Name = "Genesis",
                            SectionCode = "gen",
                            Tracks = [],
                        },
                    ],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false,
                };
                seed.BiblePublications.Add(pub);
                await seed.SaveChangesAsync();
            }

            using var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            Assert.Equal("Genesis", await sut.GetSectionNameAsync("E", "bpss-nwt", "gen"));

            var entity = await sut.GetSectionAsync("E", "bpss-nwt", "gen");
            Assert.NotNull(entity);
            Assert.Equal("Genesis", entity!.Name);
            Assert.Equal("gen", entity.SectionCode);
        }
    }

    [Fact]
    public async Task GetSectionAsync_returns_null_when_section_code_missing()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            using var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            Assert.Null(await sut.GetSectionAsync("E", "x", null!));
        }
    }

    [Fact]
    public async Task GetSectionsByPublicationAsync_skips_blank_codes_and_deduplicates_by_normalized_key()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(bibleCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "bpss-dup",
                    Name = "Dup",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                    Sections =
                    [
                        new BiblePublicationSection { Name = "Keep", SectionCode = "10", Tracks = [] },
                        new BiblePublicationSection { Name = "DropDup", SectionCode = "10", Tracks = [] },
                        new BiblePublicationSection { Name = "Blank", SectionCode = "   ", Tracks = [] },
                    ],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false,
                });
                await seed.SaveChangesAsync();
            }

            using var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            var dict = await sut.GetSectionsByPublicationAsync("E", "bpss-dup");

            var single = Assert.Single(dict);
            Assert.Equal("10", single.Key);
            Assert.Equal("Keep", single.Value.Name);
        }
    }

    [Fact]
    public async Task GetSectionsByPublicationWithoutLanguageAsync_loads_melody_style_publication()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                seed.Categories.Add(musicCat);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "iam-test",
                    Name = "Melodies",
                    LanguageId = null,
                    Language = null,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections =
                    [
                        new BiblePublicationSection { Name = "DiskA", SectionCode = "iam-1", Tracks = [] },
                        new BiblePublicationSection { Name = "DiskB", SectionCode = "iam-2", Tracks = [] },
                    ],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = true,
                });
                await seed.SaveChangesAsync();
            }

            using var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            var dict = await sut.GetSectionsByPublicationWithoutLanguageAsync("iam-test");

            Assert.Equal(2, dict.Count);
            Assert.Equal("DiskA", dict["iam-1"].Name);
            Assert.Equal("DiskB", dict["iam-2"].Name);
        }
    }

    [Fact]
    public async Task GetSectionsByPublicationWithoutLanguageAsync_keeps_first_on_duplicate_normalized_code()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                seed.Categories.Add(musicCat);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "iam-dup-sec-wave",
                    Name = "Melody dup sections",
                    LanguageId = null,
                    Language = null,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections =
                    [
                        new BiblePublicationSection { Name = "KeepDisc", SectionCode = "iam-1", Tracks = [] },
                        new BiblePublicationSection { Name = "DropDup", SectionCode = "iam-1", Tracks = [] },
                    ],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = true,
                });
                await seed.SaveChangesAsync();
            }

            using var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            var dict = await sut.GetSectionsByPublicationWithoutLanguageAsync("iam-dup-sec-wave");

            var single = Assert.Single(dict);
            Assert.Equal("iam-1", single.Key);
            Assert.Equal("KeepDisc", single.Value.Name);
        }
    }

    [Fact]
    public async Task GetSectionNameAsync_wraps_scope_errors()
    {
        var sut = new BiblePublicationSectionService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetSectionNameAsync("E", "p", "1"));

        Assert.Contains("Error getting BiblePublicationSection name", ex.Message, StringComparison.Ordinal);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }

    [Fact]
    public async Task Dispose_is_idempotent()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var sut = new BiblePublicationSectionService(factory, TestLogging.CreateLogger());

            sut.Dispose();
            Assert.Null(Record.Exception(() => sut.Dispose()));
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("test");
    }
}