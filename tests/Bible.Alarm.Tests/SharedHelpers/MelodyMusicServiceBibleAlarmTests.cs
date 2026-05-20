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

public sealed class MelodyMusicServiceBibleAlarmTests
{
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
            new MelodyMusicService(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public async Task Constructor_ThrowsWhenLoggerNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MelodyMusicService(new MediaTestScopeFactory(options), null!));
        }
    }

    [Fact]
    public async Task GetByCodeWithTracksAsync_ReturnsNull_WhenPublicationMissing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            Assert.Null(await sut.GetByCodeWithTracksAsync(AppConstants.Media.MelodyMusicPublicationCodeIam));
        }
    }

    [Fact]
    public async Task GetByCodeWithTracksAsync_Combines_Section_Tracks_And_Flat_Tracks()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var publication = new BiblePublication
                {
                    Name = "Melodies",
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    LanguageId = null,
                    Language = null,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                };

                var section = new BiblePublicationSection
                {
                    Name = "Disc 1",
                    SectionCode = "iam-1",
                    BiblePublication = publication,
                    Tracks = [],
                };
                var sectionTrack = new BiblePublicationTrack
                {
                    TrackCode = "2",
                    Title = "Section track",
                    Section = section,
                    Publication = publication,
                };
                section.Tracks.Add(sectionTrack);
                publication.Sections.Add(section);

                var flatTrack = new BiblePublicationTrack
                {
                    TrackCode = "9",
                    Title = "Flat track",
                    Publication = publication,
                    Section = null,
                    BiblePublicationSectionId = null,
                };
                publication.Tracks.Add(flatTrack);

                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();
            }

            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var result = await sut.GetByCodeWithTracksAsync(AppConstants.Media.MelodyMusicPublicationCodeIam);

            Assert.NotNull(result);
            var codes = result!.Publication!.Tracks.Select(t => t.TrackCode).OrderBy(x => x).ToArray();
            Assert.Equal(["2", "9"], codes);
            Assert.Single(result.Publication.Sections);
        }
    }

    [Fact]
    public async Task GetTracksBySectionCodeAsync_Returns_Only_Tracks_For_Matching_Section()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var publication = new BiblePublication
                {
                    Name = "Melodies",
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    LanguageId = null,
                    Language = null,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                };

                var section = new BiblePublicationSection
                {
                    Name = "Disc 2",
                    SectionCode = "iam-2",
                    BiblePublication = publication,
                    Tracks = [],
                };
                section.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "5",
                    Title = "Five",
                    Section = section,
                    Publication = publication,
                });
                publication.Sections.Add(section);

                publication.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "99",
                    Title = "Ignored for section query",
                    Publication = publication,
                    Section = null,
                    BiblePublicationSectionId = null,
                });

                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();
            }

            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var dict = await sut.GetTracksBySectionCodeAsync(
                AppConstants.Media.MelodyMusicPublicationCodeIam,
                "IAM-2");

            var track = Assert.Single(dict.Values);
            Assert.Equal("5", track.TrackCode);
            Assert.Equal("iam-2", track.DownloadCode);
        }
    }

    [Fact]
    public async Task GetTracksByCodeAsync_Builds_Ordered_Map_From_All_Tracks()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var publication = new BiblePublication
                {
                    Name = "Melodies",
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    LanguageId = null,
                    Language = null,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                };

                var section = new BiblePublicationSection
                {
                    Name = "D",
                    SectionCode = "iam-1",
                    BiblePublication = publication,
                    Tracks = [],
                };
                section.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "10",
                    Title = "Ten",
                    Section = section,
                    Publication = publication,
                });
                publication.Sections.Add(section);

                publication.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "3",
                    Title = "Three",
                    Publication = publication,
                    BiblePublicationSectionId = null,
                });

                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();
            }

            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var dict = await sut.GetTracksByCodeAsync(AppConstants.Media.MelodyMusicPublicationCodeIam);

            Assert.Equal(2, dict.Count);
            Assert.Equal("3", dict[0].TrackCode);
            Assert.Equal("10", dict[1].TrackCode);
            Assert.Equal("iam-1", dict[1].DownloadCode);
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("simulated scope failure");
    }

    [Fact]
    public async Task GetAllAsync_Keeps_First_When_Duplicate_PublicationCode()
    {
        const string dupCode = "mds-dup-melody";
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                void AddMelody(string title) =>
                    db.BiblePublications.Add(new BiblePublication
                    {
                        Name = title,
                        PublicationCode = dupCode,
                        LanguageId = null,
                        Language = null,
                        IsMusic = true,
                        IsVideo = false,
                        BiblePublicationCategories =
                        [
                            new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                        ],
                        Sections = [],
                        Tracks = [],
                    });

                AddMelody("FirstDup");
                AddMelody("SecondDup");
                await db.SaveChangesAsync();
            }

            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var map = await sut.GetAllAsync();

            Assert.Single(map);
            Assert.Equal("FirstDup", map[dupCode].Publication.Name);
        }
    }

    [Fact]
    public async Task GetTracksByCodeAsync_Deduplicates_By_TrackCode_CaseInsensitive()
    {
        const string pubCode = "mds-dedup-tc";
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var publication = new BiblePublication
                {
                    Name = "Dedup melody",
                    PublicationCode = pubCode,
                    LanguageId = null,
                    Language = null,
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
                            TrackCode = "05",
                            Title = "DupA",
                            Publication = default!,
                            BiblePublicationSectionId = null,
                        },
                        new BiblePublicationTrack
                        {
                            TrackCode = "05",
                            Title = "DupB",
                            Publication = default!,
                            BiblePublicationSectionId = null,
                        },
                    ],
                };
                publication.Tracks[0].Publication = publication;
                publication.Tracks[1].Publication = publication;

                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();
            }

            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            var dict = await sut.GetTracksByCodeAsync(pubCode);

            Assert.Single(dict);
            Assert.Equal("DupA", dict[0].Title);
        }
    }

    [Fact]
    public async Task GetTracksByCodeAsync_ReturnsEmpty_When_Publication_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());
            Assert.Empty(await sut.GetTracksByCodeAsync("no-melody-publication-wave-k"));
        }
    }

    [Fact]
    public async Task GetTracksBySectionCodeAsync_ReturnsEmpty_When_Section_Missing_Or_EmptyTracks()
    {
        const string pubCode = "mds-empty-sec";
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var publication = new BiblePublication
                {
                    Name = "Empty section shell",
                    PublicationCode = pubCode,
                    LanguageId = null,
                    Language = null,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections =
                    [
                        new BiblePublicationSection
                        {
                            Name = "No tracks",
                            SectionCode = "mds-s1",
                            Tracks = [],
                        },
                    ],
                    Tracks = [],
                };

                db.BiblePublications.Add(publication);
                await db.SaveChangesAsync();
            }

            using var sut = new MelodyMusicService(new MediaTestScopeFactory(options), TestLogging.CreateLogger());

            Assert.Empty(await sut.GetTracksBySectionCodeAsync(pubCode, "mds-s1"));
            Assert.Empty(await sut.GetTracksBySectionCodeAsync(pubCode, "no-such-section"));
            Assert.Empty(await sut.GetTracksBySectionCodeAsync("missing-pub", "mds-s1"));
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
            var sut = new MelodyMusicService(new MediaTestScopeFactory(opts), TestLogging.CreateLogger());
            sut.Dispose();
            sut.Dispose();
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetTracksByCodeAsync_Wraps_Scope_Failure()
    {
        var sut = new MelodyMusicService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetTracksByCodeAsync(AppConstants.Media.MelodyMusicPublicationCodeIam));

        Assert.Contains("Error getting melody music tracks", ex.Message, StringComparison.Ordinal);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }
}
