#nullable enable

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

public sealed class BiblePublicationServiceTests
{
    private static async Task<(MediaTestScopeFactory Inner, CountingScopeFactory Counting, SqliteConnection Connection)> CreateFactoryAsync()
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

        var inner = new MediaTestScopeFactory(options);
        return (inner, new CountingScopeFactory(inner), connection);
    }

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

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("scope failure");
    }

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BiblePublicationService(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        var (_, counting, connection) = CreateFactorySync();
        using (connection)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BiblePublicationService(counting, null!));
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithSectionsAsync_Loads_Publication_With_Sections_And_Tracks()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "bps-test-nwt",
                    Name = "New World",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections =
                    [
                        new BiblePublicationSection
                        {
                            Name = "Matthew",
                            SectionCode = "mat",
                            Tracks = []
                        }
                    ],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                };
                seed.BiblePublications.Add(pub);
                await seed.SaveChangesAsync();

                var sec = pub.Sections[0];
                sec.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "1",
                    Title = "Ch1",
                    Publication = pub,
                    BiblePublicationId = pub.Id,
                    Section = sec,
                    BiblePublicationSectionId = sec.Id,
                    TrackUrl = new TrackUrl { Url = "https://example/a.mp3" }
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var loaded = await sut.GetByLanguageAndCodeWithSectionsAsync("e", "bps-test-nwt");

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Sections);
            Assert.Single(loaded.Sections[0].Tracks);
            Assert.Equal("https://example/a.mp3", loaded.Sections[0].Tracks[0].TrackUrl!.Url);
            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithSectionsAsync_Second_Call_Reuses_Cache_Within_Ttl()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "bps-cache-pub",
                    Name = "Cached",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetByLanguageAndCodeWithSectionsAsync("E", "bps-cache-pub");
            _ = await sut.GetByLanguageAndCodeWithSectionsAsync("E", "bps-cache-pub");

            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task InvalidatePublicationCaches_Forces_New_Load()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "bps-inv-pub",
                    Name = "Inv",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetByLanguageAndCodeWithSectionsAsync("E", "bps-inv-pub");
            sut.InvalidatePublicationCaches("E", "bps-inv-pub");
            _ = await sut.GetByLanguageAndCodeWithSectionsAsync("E", "bps-inv-pub");

            Assert.Equal(2, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithSectionsAsync_Wraps_Scope_Failure()
    {
        var sut = new BiblePublicationService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetByLanguageAndCodeWithSectionsAsync("E", "any"));

        Assert.Contains("Error getting BiblePublication with Sections", ex.Message, StringComparison.Ordinal);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }

    [Fact]
    public async Task GetByLanguageCodeAsync_Keeps_First_When_Duplicate_Publication_Code()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "DupPub",
                    Name = "First",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                });
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "DupPub",
                    Name = "Second",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var map = await sut.GetByLanguageCodeAsync("E");

            Assert.Single(map);
            Assert.Equal("First", map["DupPub"].Name);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithTracksAsync_Deduplicates_Tracks_By_TrackCode()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "flat-dup-tracks",
                    Name = "Flat",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                };
                seed.BiblePublications.Add(pub);
                await seed.SaveChangesAsync();

                pub.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "7",
                    Title = "A",
                    Publication = pub,
                    BiblePublicationId = pub.Id,
                    BiblePublicationSectionId = null,
                    TrackUrl = new TrackUrl { Url = "https://a.mp3" }
                });
                pub.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "7",
                    Title = "B",
                    Publication = pub,
                    BiblePublicationId = pub.Id,
                    BiblePublicationSectionId = null,
                    TrackUrl = new TrackUrl { Url = "https://b.mp3" }
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var loaded = await sut.GetByLanguageAndCodeWithTracksAsync("E", "flat-dup-tracks");

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Tracks);
            Assert.Equal("A", loaded.Tracks[0].Title);
            Assert.Same(loaded, loaded.Tracks[0].Publication);
        }
    }

    [Fact]
    public async Task GetDistinctLanguagesAsync_NoCategory_Second_Call_Uses_Cached_AllLanguages()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var lang = new Language { LanguageCode = "X", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(bibleCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "pl-all-lang",
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetDistinctLanguagesAsync();
            Assert.Equal(1, counting.CreateScopeCallCount);

            _ = await sut.GetDistinctLanguagesAsync();
            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetDistinctLanguagesAsync_Music_With_IsMusicFilter_Second_Call_Uses_Separate_Cache_Key()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(musicCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "vocal-pl",
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetDistinctLanguagesAsync(
                AppConstants.Media.BiblePublicationCategoryMusic,
                filterIsMusicWhenMusicCategory: true);
            Assert.Equal(1, counting.CreateScopeCallCount);

            _ = await sut.GetDistinctLanguagesAsync(
                AppConstants.Media.BiblePublicationCategoryMusic,
                filterIsMusicWhenMusicCategory: true);
            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetDistinctLanguagesAsync_Second_Call_Uses_Cache()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var lang = new Language { LanguageCode = "M", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(cat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "pl-x",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetDistinctLanguagesAsync(AppConstants.Media.BiblePublicationCategoryBible);
            Assert.Equal(1, counting.CreateScopeCallCount);

            _ = await sut.GetDistinctLanguagesAsync(AppConstants.Media.BiblePublicationCategoryBible);
            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetAvailablePublicationCodesAsync_Returns_Distinct_Sorted_Codes()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(cat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "zebra",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "Alpha",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var codes = await sut.GetAvailablePublicationCodesAsync("E", AppConstants.Media.BiblePublicationCategoryBible);

            Assert.Equal(["Alpha", "zebra"], codes);
        }
    }

    [Fact]
    public async Task GetFirstPublicationCodeByOrderAsync_Returns_Lowest_Id_Row()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(cat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "second",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true
                });
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "first",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var code = await sut.GetFirstPublicationCodeByOrderAsync("E", AppConstants.Media.BiblePublicationCategoryMusic, filterIsMusicWhenMusicCategory: true);

            Assert.Equal("second", code);
        }
    }

    [Fact]
    public async Task IsNoLanguagePublicationAsync_Returns_True_When_Row_Has_Null_Language()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                seed.Categories.Add(bibleCat);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "iam-flat",
                    Name = "Melody",
                    LanguageId = null,
                    Language = null,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            Assert.True(await sut.IsNoLanguagePublicationAsync("iam-flat"));
        }
    }

    [Fact]
    public async Task GetPublicationCategoryInfoAsync_Returns_From_Cataloged_Publication()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(musicCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "song-123",
                    Name = "Songs",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var info = await sut.GetPublicationCategoryInfoAsync("E", "song-123");

            Assert.NotNull(info);
            Assert.Equal(AppConstants.Media.BiblePublicationCategoryMusic, info!.Value.CategoryCode);
            Assert.True(info.Value.IsMusic);
        }
    }

    [Fact]
    public async Task GetPublicationCategoryInfoAsync_From_PublicationLanguage_When_Not_Cataloged()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(musicCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "discovery-only-cc",
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var info = await sut.GetPublicationCategoryInfoAsync("E", "discovery-only-cc");

            Assert.NotNull(info);
            Assert.Equal(AppConstants.Media.BiblePublicationCategoryMusic, info!.Value.CategoryCode);
            Assert.True(info.Value.IsMusic);
        }
    }

    [Fact]
    public async Task GetPublicationCategoryInfoAsync_ForMakingMusic_Pl_Inference_Has_IsMusic_False_Even_With_Music_Category()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(musicCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.MediatorPublicationCodeMakingMusic,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var info = await sut.GetPublicationCategoryInfoAsync("E", AppConstants.Media.MediatorPublicationCodeMakingMusic);

            Assert.NotNull(info);
            Assert.Equal(AppConstants.Media.BiblePublicationCategoryMusic, info!.Value.CategoryCode);
            Assert.False(info.Value.IsMusic);
        }
    }

    [Fact]
    public async Task GetByLanguageCodeAsync_When_Music_Category_And_FilterIsMusic_Includes_IsMusic_Publications_Only()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(musicCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "not-music-song-list",
                    Name = "A",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                });
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "music-song-list",
                    Name = "B",
                    Language = lang,
                    LanguageId = lang.Id,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var map = await sut.GetByLanguageCodeAsync(
                "E",
                AppConstants.Media.BiblePublicationCategoryMusic,
                filterIsMusicWhenMusicCategory: true);

            Assert.Single(map);
            Assert.True(map.TryGetValue("music-song-list", out var pub));
            Assert.Equal("B", pub.Name);
        }
    }

    [Fact]
    public async Task GetAvailablePublicationCodesAsync_Includes_PublicationLanguage_Rows_With_Null_LanguageId()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(cat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "lang-backed",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "no-lang-id-row",
                    Category = cat,
                    CategoryId = cat.Id,
                    LanguageId = null,
                    Language = null,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var codes = await sut.GetAvailablePublicationCodesAsync("E", AppConstants.Media.BiblePublicationCategoryMusic);

            Assert.Contains("lang-backed", codes);
            Assert.Contains("no-lang-id-row", codes);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithTracksAsync_Second_Call_Reuses_Cache_Within_Ttl()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "bps-tracks-cache",
                    Name = "Tracks cached",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetByLanguageAndCodeWithTracksAsync("E", "bps-tracks-cache");
            _ = await sut.GetByLanguageAndCodeWithTracksAsync("E", "bps-tracks-cache");

            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithTracksAsync_Canonicalizes_Mediator_Publication_Code_For_Query_And_Invalidate()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
                    Name = "Canonical drama",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            _ = await sut.GetByLanguageAndCodeWithTracksAsync("E", "vodmoviesbibletimes");

            sut.InvalidatePublicationCaches("e", "VODmoviesBIBLEtimes");

            _ = await sut.GetByLanguageAndCodeWithTracksAsync("E", "vodmoviesbibletimes");

            Assert.Equal(2, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetPublicationCodesInCategoryOrderAsync_Unions_Discovery_And_Catalog()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                seed.Categories.Add(cat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = "only-in-pl",
                    Category = cat,
                    CategoryId = cat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = false
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "only-in-bp",
                    Name = "B",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var ordered = await sut.GetPublicationCodesInCategoryOrderAsync(
                "E",
                AppConstants.Media.BiblePublicationCategoryBible);

            Assert.Contains("only-in-pl", ordered);
            Assert.Contains("only-in-bp", ordered);
        }
    }

    [Fact]
    public async Task GetPublicationCategoryInfoAsync_Reads_From_No_Language_Shell_When_Languaged_Publication_Missing()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "bps-no-lang-shell",
                    Name = "Shell without language row",
                    LanguageId = null,
                    Language = null,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            var info = await sut.GetPublicationCategoryInfoAsync("E", "bps-no-lang-shell");

            Assert.NotNull(info);
            Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, info!.Value.CategoryCode);
            Assert.False(info.Value.IsMusic);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithSectionsAsync_PreCanceledToken_Evicts_Cache_Then_Unblocked_Call_Succeeds()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
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
                    PublicationCode = "bps-pre-cancel-retry",
                    Name = "Cancel then OK",
                    Language = lang,
                    LanguageId = lang.Id,
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

            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetByLanguageAndCodeWithSectionsAsync("E", "bps-pre-cancel-retry", canceled.Token));

            Assert.Contains("Error getting BiblePublication with Sections", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(ex.InnerException);

            var loaded = await sut.GetByLanguageAndCodeWithSectionsAsync("E", "bps-pre-cancel-retry");
            Assert.NotNull(loaded);
            Assert.Equal("Cancel then OK", loaded!.Name);
            Assert.True(counting.CreateScopeCallCount >= 2);
        }
    }

    [Fact]
    public async Task GetByLanguageAndCodeWithTracksAsync_Wraps_Scope_Failure()
    {
        var sut = new BiblePublicationService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetByLanguageAndCodeWithTracksAsync("E", "any"));

        Assert.Contains("Error getting BiblePublication with Tracks", ex.Message, StringComparison.Ordinal);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }

    [Fact]
    public async Task GetDistinctLanguagesAsync_Wraps_Scope_Failure()
    {
        var sut = new BiblePublicationService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetDistinctLanguagesAsync());

        Assert.Contains("distinct Languages", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }

    [Fact]
    public async Task GetPublicationCodesInCategoryOrderAsync_Wraps_Scope_Failure()
    {
        var sut = new BiblePublicationService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetPublicationCodesInCategoryOrderAsync("E", AppConstants.Media.BiblePublicationCategoryBible));

        Assert.Contains("category order", ex.Message, StringComparison.OrdinalIgnoreCase);
        var innerIo = Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.IsType<DivideByZeroException>(innerIo.InnerException);
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var (_, counting, connection) = CreateFactorySync();
        using (connection)
        {
            var sut = new BiblePublicationService(counting, TestLogging.CreateLogger());
            sut.Dispose();
            sut.Dispose();
        }
    }

    private static (MediaTestScopeFactory Inner, CountingScopeFactory Counting, SqliteConnection Connection) CreateFactorySync()
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

        var inner = new MediaTestScopeFactory(options);
        return (inner, new CountingScopeFactory(inner), connection);
    }
}
