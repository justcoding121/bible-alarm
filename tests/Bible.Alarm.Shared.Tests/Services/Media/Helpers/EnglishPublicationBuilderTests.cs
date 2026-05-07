#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class EnglishPublicationBuilderTests
{
    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new EnglishPublicationBuilder(null!));
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_ReturnsFalse_When_Sections_Empty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);
        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-0",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var req = new BuildEnglishPublicationRequest(
            db,
            AppConstants.Media.BiblePublicationCodeNwt,
            PublicationName: "NWT",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: [],
            CancellationToken: CancellationToken.None);

        Assert.False(await sut.BuildAndSavePublicationAsync(req));
        Assert.Equal(0, await db.BiblePublications.CountAsync());
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_Inserts_Then_Updates_Sections_For_Same_Publication()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var sectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber;

        var firstSections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis first pass",
                SectionCode = sectionCode,
                Tracks = [],
            },
        };

        var reqInsert = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "First label",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: firstSections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(reqInsert));

        var secondSections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis second pass",
                SectionCode = sectionCode,
                Tracks = [],
            },
        };

        var reqUpdate = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Second label",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: secondSections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(reqUpdate));

        var persisted = await db.BiblePublications
            .Include(p => p.Sections)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);

        Assert.Equal("Second label", persisted.Name);
        var section = Assert.Single(persisted.Sections);
        Assert.Equal("Genesis second pass", section.Name);
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_UsesNormalizedCode_AsName_When_PublicationName_Null()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-2",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var sectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber;
        var sections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis",
                SectionCode = sectionCode,
                Tracks = [],
            },
        };

        var req = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: null,
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: sections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(req));

        var persisted = await db.BiblePublications.SingleAsync(p =>
            p.PublicationCode == pubCode && p.LanguageId == lang.Id);
        Assert.Equal(pubCode, persisted.Name);
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_BiblePublication_Restores_StagedTrack_ForeignKeys_OnInsert()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-3",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber;
        var sections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis",
                SectionCode = sectionCode,
                Tracks =
                [
                    new BiblePublicationTrack
                    {
                        TrackCode = "1",
                        Title = "Genesis 1",
                    },
                ],
            },
        };

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var req = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "NWT",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: sections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(req));

        var persisted = await db.BiblePublications
            .Include(p => p.Sections)
            .ThenInclude(s => s.Tracks)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);
        var section = Assert.Single(persisted.Sections);
        var track = Assert.Single(section.Tracks);
        var pubId = persisted.Id;
        Assert.True(track.BiblePublicationId > 0);
        Assert.Equal(pubId, track.BiblePublicationId);
        Assert.True(track.BiblePublicationSectionId.HasValue);
        Assert.Equal(section.Id, track.BiblePublicationSectionId!.Value);
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_Update_Removes_TrackUrls_From_Replaced_Tracks_ForNonBiblePublication()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.NormalizedPublicationCodeDramasGoodNews;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-4",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sectionCodeFirst = "drama-a";
        var firstSections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Act I",
                SectionCode = sectionCodeFirst,
                Tracks =
                [
                    new BiblePublicationTrack
                    {
                        TrackCode = "1",
                        Title = "Pilot",
                        TrackUrl = new TrackUrl { Url = "https://cdn/first-track.mp4" },
                    },
                ],
            },
        };

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var reqInsert = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Drama v1",
            Language: lang,
            IsVideo: true,
            IsBible: false,
            PublicationWithoutLanguage: false,
            Sections: firstSections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(reqInsert));
        Assert.Equal(1, await db.TrackUrls.CountAsync());

        var secondSections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Act II",
                SectionCode = "drama-b",
                Tracks =
                [
                    new BiblePublicationTrack { TrackCode = "2", Title = "Episode 2" },
                ],
            },
        };

        var reqUpdate = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Drama v2",
            Language: lang,
            IsVideo: true,
            IsBible: false,
            PublicationWithoutLanguage: false,
            Sections: secondSections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(reqUpdate));

        Assert.Equal(0, await db.TrackUrls.CountAsync());
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_SeriesBjfSongs_Sets_IsMusic()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.MediatorPublicationCodeSeriesBJFSongs.ToLowerInvariant();
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-5",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var sections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Song A",
                SectionCode = "s1",
                Tracks = [],
            },
        };

        var req = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "BJF Songs",
            Language: lang,
            IsVideo: false,
            IsBible: false,
            PublicationWithoutLanguage: false,
            Sections: sections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(req));

        var persisted = await db.BiblePublications.SingleAsync(p =>
            p.PublicationCode == pubCode && p.LanguageId == lang.Id);
        Assert.True(persisted.IsMusic);
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_Update_Adds_SecondCategory_When_Missing_On_First_Insert()
    {
        var pubCode = AppConstants.Media.BiblePublicationCodeSeriesBJFLessons;
        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(pubCode).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(categoryCodes.Count >= 2);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        db.Categories.Add(new Category { CategoryCode = categoryCodes[0] });
        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-BIF",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var sectionsFirst = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "First",
                SectionCode = "bjf-lesson-one",
                Tracks = [],
            },
        };

        Assert.True(await sut.BuildAndSavePublicationAsync(new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Lessons pass 1",
            Language: lang,
            IsVideo: false,
            IsBible: false,
            PublicationWithoutLanguage: false,
            Sections: sectionsFirst,
            CancellationToken: CancellationToken.None)));

        db.Categories.Add(new Category { CategoryCode = categoryCodes[1] });
        await db.SaveChangesAsync();

        var sectionsSecond = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Second pass",
                SectionCode = "bjf-lesson-two",
                Tracks = [],
            },
        };

        Assert.True(await sut.BuildAndSavePublicationAsync(new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Lessons pass 2",
            Language: lang,
            IsVideo: false,
            IsBible: false,
            PublicationWithoutLanguage: false,
            Sections: sectionsSecond,
            CancellationToken: CancellationToken.None)));

        var persisted = await db.BiblePublications
            .Include(p => p.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);
        Assert.Equal(2, persisted.BiblePublicationCategories.Count);
        var linkedCodes = persisted.BiblePublicationCategories
            .Select(bpc => bpc.Category!.CategoryCode)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(categoryCodes, linkedCodes);
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_BibleWithoutLanguage_inserts_Tracks_With_Publication()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-NWL",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber;
        var sections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis",
                SectionCode = sectionCode,
                Tracks =
                [
                    new BiblePublicationTrack
                    {
                        TrackCode = "1",
                        Title = "Genesis 1",
                    },
                ],
            },
        };

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        Assert.True(await sut.BuildAndSavePublicationAsync(new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "NWL",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: true,
            Sections: sections,
            CancellationToken: CancellationToken.None)));

        var persisted = await db.BiblePublications
            .Include(p => p.Sections)
            .ThenInclude(s => s.Tracks)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);
        var section = Assert.Single(persisted.Sections);
        var track = Assert.Single(section.Tracks);
        Assert.NotNull(track.Publication);
        Assert.True(track.BiblePublicationId > 0);
        Assert.True(track.BiblePublicationSectionId.HasValue);
        Assert.Equal(section.Id, track.BiblePublicationSectionId!.Value);
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_Update_BibleWithoutLanguage_Refreshes_Sections_Without_AssignFk_Pass()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-NWU",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber;
        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());

        Assert.True(await sut.BuildAndSavePublicationAsync(new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Pass 1",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: true,
            Sections:
            [
                new BiblePublicationSection
                {
                    Name = "Genesis A",
                    SectionCode = sectionCode,
                    Tracks =
                    [
                        new BiblePublicationTrack { TrackCode = "a", Title = "A" },
                    ],
                },
            ],
            CancellationToken: CancellationToken.None)));

        Assert.True(await sut.BuildAndSavePublicationAsync(new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Pass 2",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: true,
            Sections:
            [
                new BiblePublicationSection
                {
                    Name = "Genesis B",
                    SectionCode = sectionCode,
                    Tracks =
                    [
                        new BiblePublicationTrack { TrackCode = "b", Title = "B" },
                    ],
                },
            ],
            CancellationToken: CancellationToken.None)));

        var persisted = await db.BiblePublications
            .Include(p => p.Sections)
            .ThenInclude(s => s.Tracks)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);
        var section = Assert.Single(persisted.Sections);
        Assert.Equal("Genesis B", section.Name);
        var track = Assert.Single(section.Tracks);
        Assert.Equal("b", track.TrackCode);
        Assert.True(track.BiblePublicationId > 0);
    }
}
