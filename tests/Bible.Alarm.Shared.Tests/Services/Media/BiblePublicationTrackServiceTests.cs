#nullable enable

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

public sealed class BiblePublicationTrackServiceTests
{
    private static (MediaTestScopeFactory Factory, SqliteConnection Connection) CreateFactory()
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
    public void Constructor_rejects_null_scope_factory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BiblePublicationTrackService(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_rejects_null_logger()
    {
        var (factory, connection) = CreateFactory();
        using (connection)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BiblePublicationTrackService(factory, null!));
        }
    }

    [Fact]
    public async Task GetTracksBySectionAsync_returns_empty_when_publication_missing()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            var result = await sut.GetTracksBySectionAsync("E", "missing", "sec");
            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task GetTracksBySectionAsync_orders_track_codes_via_comparer()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var db = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "E",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "P",
                    PublicationCode = "nwt",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                };
                var section = new BiblePublicationSection
                {
                    Name = "Matthew",
                    SectionCode = "mat",
                    BiblePublication = pub,
                };
                pub.Sections.Add(section);

                section.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "20",
                    Title = "Last",
                    Publication = pub,
                    Section = section,
                });

                section.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "3",
                    Title = "First",
                    Publication = pub,
                    Section = section,
                });

                section.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "10",
                    Title = "Middle",
                    Publication = pub,
                    Section = section,
                });

                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();
            }

            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            var map = await sut.GetTracksBySectionAsync("e", "nwt", "mat");

            Assert.Equal(3, map.Count);
            Assert.Equal(new[] { "3", "10", "20" }, map.Keys.ToArray());
        }
    }

    [Fact]
    public async Task GetTracksBySectionAsync_keeps_first_when_duplicate_track_code_on_flat_tracks()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var db = new MediaDbContext(opts))
            {
                var lang = new Language
                {
                    LanguageCode = "E",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                db.Languages.Add(lang);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "Flat dup codes",
                    PublicationCode = "bpts-dup-flat",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                };

                pub.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "7",
                    Title = "FirstSeven",
                    Publication = pub,
                    Section = null,
                    BiblePublicationSectionId = null,
                });
                pub.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "7",
                    Title = "SecondSevenSkipped",
                    Publication = pub,
                    Section = null,
                    BiblePublicationSectionId = null,
                });

                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();
            }

            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            var map = await sut.GetTracksBySectionAsync("E", "bpts-dup-flat", "   ");

            Assert.Single(map);
            Assert.Equal("FirstSeven", map["7"].Title);
        }
    }

    [Fact]
    public async Task GetTracksBySectionAsync_whitespace_section_loads_publication_level_tracks()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var db = new MediaDbContext(opts))
            {
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                db.Languages.Add(lang);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "No section pub",
                    PublicationCode = "z1",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                };

                pub.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "1",
                    Title = "Whole pub",
                    Publication = pub,
                    Section = null,
                    BiblePublicationSectionId = null,
                });

                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();
            }

            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            var none = await sut.GetTracksBySectionAsync("E", "z1", "mat");

            Assert.Empty(none);

            var flat = await sut.GetTracksBySectionAsync("E", "z1", "   ");

            Assert.Single(flat);
            Assert.Equal("1", flat.Keys.First());
        }
    }

    [Fact]
    public async Task GetTrackAsync_returns_hit_or_null()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var db = new MediaDbContext(opts))
            {
                var lang = new Language { LanguageCode = "MY", Direction = AppConstants.Media.TextDirectionLeftToRight };
                db.Languages.Add(lang);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "P2",
                    PublicationCode = "nw",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                };

                var section = new BiblePublicationSection
                {
                    SectionCode = "gen",
                    Name = "Genesis",
                    BiblePublication = pub,
                };
                pub.Sections.Add(section);

                section.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "5",
                    Title = "Hit",
                    Publication = pub,
                    Section = section,
                });

                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();
            }

            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());

            Assert.Null(await sut.GetTrackAsync("my", "nw", "gen", "99"));

            var hit = await sut.GetTrackAsync("my", "nw", "gen", "5");
            Assert.NotNull(hit);
            Assert.Equal("Hit", hit!.Title);
        }
    }

    [Fact]
    public async Task UpdateTrackUrlAsync_completes_without_throw()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            await sut.UpdateTrackUrlAsync("E", "x", null, "1", "https://example/x");
        }
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var (factory, connection) = CreateFactory();
        using (connection)
        {
            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            sut.Dispose();
            sut.Dispose();
        }
    }

    [Fact]
    public async Task GetTrackAsync_wraps_scope_errors()
    {
        var throwing = new ThrowingScopeFactory();
        var sut = new BiblePublicationTrackService(throwing, TestLogging.CreateLogger());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetTrackAsync("E", "p", null, "1"));
    }

    [Fact]
    public async Task GetTracksBySectionAsync_wraps_scope_errors()
    {
        var sut = new BiblePublicationTrackService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetTracksBySectionAsync("E", "p", null));
    }

    [Fact]
    public async Task GetTrackAsync_whitespace_section_treats_as_flat_track_query()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var db = new MediaDbContext(opts))
            {
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                db.Languages.Add(lang);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "Flat only",
                    PublicationCode = "flax-w2",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                };
                pub.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "42",
                    Title = "Ambient",
                    Publication = pub,
                    Section = null,
                    BiblePublicationSectionId = null,
                });
                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();
            }

            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());
            var hit = await sut.GetTrackAsync("E", "flax-w2", "   ", "42");
            Assert.NotNull(hit);
            Assert.Equal("Ambient", hit!.Title);
        }
    }

    [Fact]
    public async Task GetTrackAsync_returns_null_when_track_not_in_given_section()
    {
        var (factory, connection) = CreateFactory();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var db = new MediaDbContext(opts))
            {
                var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
                db.Languages.Add(lang);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "Sec pub",
                    PublicationCode = "sec-w2",
                    LanguageId = lang.Id,
                    Language = lang,
                    IsVideo = false,
                    IsMusic = false,
                };
                var secGen = new BiblePublicationSection { Name = "G", SectionCode = "gen", BiblePublication = pub };
                pub.Sections.Add(secGen);
                secGen.Tracks.Add(new BiblePublicationTrack
                {
                    TrackCode = "9",
                    Title = "Nine",
                    Publication = pub,
                    Section = secGen,
                });

                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();
            }

            var sut = new BiblePublicationTrackService(factory, TestLogging.CreateLogger());

            Assert.Null(await sut.GetTrackAsync("e", "sec-w2", "mat", "9"));
            var ok = await sut.GetTrackAsync("e", "sec-w2", "gen", "9");
            Assert.NotNull(ok);
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("test");
    }
}
