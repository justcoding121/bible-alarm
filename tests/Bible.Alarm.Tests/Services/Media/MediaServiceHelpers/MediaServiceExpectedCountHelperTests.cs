#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Services.Media.MediaServiceHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceExpectedCountHelperTests
{
    private static DbContextOptions<MediaDbContext> SqliteMemoryOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task GetExpectedSectionCountForNoLanguagePublicationAsync_counts_distinct_sections_with_null_LanguageId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pubCode = "iam-nolang";

        await using (var seed = new MediaDbContext(options))
        {
            var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            seed.Categories.Add(cat);

            var pl = new PublicationLanguage
            {
                PublicationCode = pubCode,
                Category = cat,
                LanguageId = null,
                Language = null,
                IsMusic = true,
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "iams-a",
                LanguageId = null,
                PublicationLanguage = pl,
            });
            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "iams-b",
                LanguageId = null,
                PublicationLanguage = pl,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var count = await MediaServiceExpectedCountHelper.GetExpectedSectionCountForNoLanguagePublicationAsync(factory,
            pubCode, CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetExpectedSectionCountAsync_with_language_includes_null_language_and_matching_code()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pubCode = "nwt";
        await using (var seed = new MediaDbContext(options))
        {
            var english = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Languages.Add(english);
            await seed.SaveChangesAsync();

            var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
            seed.Categories.Add(cat);
            var withLang = new PublicationLanguage
            {
                PublicationCode = pubCode,
                Category = cat,
                LanguageId = english.Id,
                Language = english,
            };
            var noLang = new PublicationLanguage
            {
                PublicationCode = pubCode,
                Category = cat,
                LanguageId = null,
            };
            seed.PublicationLanguages.AddRange(withLang, noLang);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "1",
                LanguageId = english.Id,
                Language = english,
                PublicationLanguage = withLang,
            });
            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "2",
                LanguageId = null,
                PublicationLanguage = noLang,
            });
            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "2",
                LanguageId = null,
                PublicationLanguage = noLang,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var withLanguage = await MediaServiceExpectedCountHelper.GetExpectedSectionCountAsync(
            factory, "e", pubCode, CancellationToken.None);
        var blankLanguage = await MediaServiceExpectedCountHelper.GetExpectedSectionCountAsync(
            factory, "  ", pubCode, CancellationToken.None);

        Assert.Equal(2, withLanguage);
        Assert.Equal(1, blankLanguage);
    }

    [Fact]
    public async Task GetExpectedPublicationCountAsync_filters_music_and_collapses_drama_codes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var music = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var dramas = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryDramas };
            seed.Categories.AddRange(music, dramas);

            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = "osg",
                Category = music,
                LanguageId = null,
                IsMusic = true,
            });
            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = "talk",
                Category = music,
                LanguageId = null,
                IsMusic = false,
            });
            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = "DRAMAS",
                Category = dramas,
                LanguageId = null,
            });
            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = "dramas",
                Category = dramas,
                LanguageId = null,
            });
            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = AppConstants.Media.BiblePublicationCodeDramaticBibleReadings,
                Category = dramas,
                LanguageId = null,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var musicUnfiltered = await MediaServiceExpectedCountHelper.GetExpectedPublicationCountAsync(
            factory, null, AppConstants.Media.BiblePublicationCategoryMusic, CancellationToken.None);
        var musicOnly = await MediaServiceExpectedCountHelper.GetExpectedPublicationCountAsync(
            factory,
            null,
            AppConstants.Media.BiblePublicationCategoryMusic,
            CancellationToken.None,
            requireIsMusicForMusicCategory: true);
        var dramaCount = await MediaServiceExpectedCountHelper.GetExpectedPublicationCountAsync(
            factory, null, AppConstants.Media.BiblePublicationCategoryDramas, CancellationToken.None);

        Assert.Equal(2, musicUnfiltered);
        Assert.Equal(1, musicOnly);
        Assert.Equal(2, dramaCount);
    }
}
