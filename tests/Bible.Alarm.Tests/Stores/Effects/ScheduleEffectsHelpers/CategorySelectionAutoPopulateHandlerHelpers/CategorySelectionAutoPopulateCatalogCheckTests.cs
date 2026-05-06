#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.CategorySelectionAutoPopulateHandlerHelpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class CategorySelectionAutoPopulateCatalogCheckTests
{
    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_returns_false_when_missing_publication()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        Assert.False(await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            TestLogging.CreateLogger(),
            db,
            publicationCode: "nwt",
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_returns_true_when_first_section_has_track()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var bibleCat = new Category { CategoryCode = "Bible" };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(bibleCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var bp = new BiblePublication
            {
                Name = "NWT",
                PublicationCode = "nwt",
                Language = lang,
                LanguageId = lang.Id,
                IsVideo = false,
                IsMusic = false,
            };
            var sec = new BiblePublicationSection
            {
                Name = "Genesis",
                SectionCode = "gen",
                BiblePublication = bp,
            };
            bp.Sections.Add(sec);
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();

            var pl = new PublicationLanguage
            {
                PublicationCode = "nwt",
                Category = bibleCat,
                Language = lang,
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = "nwt",
                SectionCode = "gen",
                Language = lang,
                PublicationLanguage = pl,
            });

            seed.BiblePublicationTracks.Add(new BiblePublicationTrack
            {
                TrackCode = "1",
                Title = "One",
                Publication = bp,
                Section = sec,
            });

            await seed.SaveChangesAsync();
        }

        await using var db = new MediaDbContext(options);

        Assert.True(await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            TestLogging.CreateLogger(),
            db,
            publicationCode: "nwt",
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_dramas_maps_to_category_row_and_flat_tracks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var bibleCat = new Category { CategoryCode = "Bible" };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(bibleCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var bp = new BiblePublication
            {
                Name = "Audio Dramas",
                PublicationCode = AppConstants.Media.BiblePublicationCategoryDramas,
                Language = lang,
                LanguageId = lang.Id,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "iaey",
                Title = "Episode",
                Publication = bp,
                BiblePublicationSectionId = null,
            });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        await using var db = new MediaDbContext(options);

        Assert.True(await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            TestLogging.CreateLogger(),
            db,
            publicationCode: "dramas",
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_non_dramas_drama_code_maps_to_dramatic_bible_readings_row()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var canonical = AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;

        await using (var seed = new MediaDbContext(options))
        {
            var bibleCat = new Category { CategoryCode = "Bible" };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(bibleCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var bp = new BiblePublication
            {
                Name = "Dramatic Bible Readings",
                PublicationCode = canonical,
                Language = lang,
                LanguageId = lang.Id,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "dbr-1",
                Title = "Reading",
                Publication = bp,
                BiblePublicationSectionId = null,
            });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        await using var db = new MediaDbContext(options);

        Assert.True(await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            TestLogging.CreateLogger(),
            db,
            publicationCode: canonical,
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_true_when_no_section_languages_but_flat_track_exists()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var bibleCat = new Category { CategoryCode = "Bible" };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(bibleCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var bp = new BiblePublication
            {
                Name = "NWT",
                PublicationCode = "nwt",
                Language = lang,
                LanguageId = lang.Id,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "flat-1",
                Title = "Flat",
                Publication = bp,
                BiblePublicationSectionId = null,
            });
            seed.BiblePublications.Add(bp);

            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = "nwt",
                Category = bibleCat,
                Language = lang,
            });

            await seed.SaveChangesAsync();
        }

        await using var db = new MediaDbContext(options);

        Assert.True(await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            TestLogging.CreateLogger(),
            db,
            publicationCode: "nwt",
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task CheckIfPublicationWithFirstSectionCatalogedAsync_returns_false_when_db_disposed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var db = new MediaDbContext(options);
        await db.DisposeAsync();

        Assert.False(await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
            TestLogging.CreateLogger(),
            db,
            publicationCode: "nwt",
            normalizedLanguageCode: "E"));
    }
}
