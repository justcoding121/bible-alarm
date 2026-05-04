#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;
using Bible.Alarm.Stores.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class MusicCascadeModalCountHelperTests
{
    [Fact]
    public async Task GetMusicPublicationModalItemCountAsync_defaults_language_and_counts_distinct_codes()
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
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(musicCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            foreach (var code in new[] { "modal-a", "modal-b" })
            {
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = code,
                    Category = musicCat,
                    Language = lang,
                    IsMusic = true,
                });
            }

            await seed.SaveChangesAsync();
        }

        await using var db = new MediaDbContext(options);

        var schedule = new ScheduleStateItem { MusicLanguageCode = null };

        Assert.Equal(2, await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, schedule));
    }

    [Fact]
    public async Task GetMusicSectionModalItemCountAsync_returns_zero_when_publication_blank_or_flat_music()
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

        Assert.Equal(0, await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db,
            new ScheduleStateItem { MusicPublicationCode = " " }));

        var flatVocalCode = JwSourceHelper.VocalMusicPublicationCodes.First();
        Assert.Equal(0, await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db,
            new ScheduleStateItem { MusicPublicationCode = flatVocalCode }));
    }

    [Fact]
    public async Task GetMusicSectionModalItemCountAsync_counts_distinct_sections_for_sectioned_publication()
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
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(musicCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var pl = new PublicationLanguage
            {
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                Category = musicCat,
                Language = lang,
                IsMusic = true,
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.AddRange(
                new SectionLanguage
                {
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    SectionCode = "iam-1-a",
                    Language = lang,
                    PublicationLanguage = pl,
                },
                new SectionLanguage
                {
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    SectionCode = "iam-1-b",
                    Language = lang,
                    PublicationLanguage = pl,
                });

            await seed.SaveChangesAsync();
        }

        await using var db = new MediaDbContext(options);

        var schedule = new ScheduleStateItem
        {
            MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            MusicLanguageCode = AppConstants.Media.DefaultLanguageCode,
        };

        Assert.Equal(2, await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, schedule));
    }
}
