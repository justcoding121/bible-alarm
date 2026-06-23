#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Tests.Support;

/// <summary>
/// Builds file-backed schedule and media index SQLite databases for bootstrap/copy integration tests.
/// </summary>
internal static class MediaBootstrapTestDatabaseHelper
{
    internal const string FrenchLanguageCode = "F";
    internal const string SamplePublicationCode = "nwt";
    internal const string SampleSectionCode = "gen";
    internal const string SampleTrackCode = "1";
    internal const string SampleTrackUrl = "https://cdn.example/f-gen-1.mp3";

    internal static async Task<string> CreateTempDirectoryAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bible-alarm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    internal static async Task<string> CreateScheduleDbWithFrenchReferenceAsync(string directory)
    {
        var path = Path.Combine(directory, "schedule.db");
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite($"Data Source={path}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = new ScheduleDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var alarm = new AlarmSchedule
        {
            Name = "French Morning",
            Hour = 7,
            Minute = 0,
            DaysOfWeek = WeekDays.Monday,
            IsEnabled = true,
        };
        db.AlarmSchedules.Add(alarm);
        await db.SaveChangesAsync();

        db.BiblePublicationSchedules.Add(new BiblePublicationSchedule
        {
            AlarmScheduleId = alarm.Id,
            AlarmSchedule = alarm,
            PublicationCode = SamplePublicationCode,
            LanguageCode = FrenchLanguageCode,
            SectionCode = SampleSectionCode,
            TrackCode = SampleTrackCode,
            FinishedDuration = TimeSpan.Zero,
        });
        await db.SaveChangesAsync();

        return path;
    }

    internal static async Task<string> CreateEmptyMediaIndexDbAsync(string directory, string fileName = "mediaIndex.db")
    {
        var path = Path.Combine(directory, fileName);
        var options = CreateMediaOptions(path);
        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return path;
    }

    internal static async Task SeedFrenchDiscoveryAsync(string mediaIndexPath)
    {
        var options = CreateMediaOptions(mediaIndexPath);
        await using var db = new MediaDbContext(options);

        var category = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
        var language = new Language
        {
            LanguageCode = FrenchLanguageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Categories.Add(category);
        db.Languages.Add(language);
        await db.SaveChangesAsync();

        var pubLang = new PublicationLanguage
        {
            PublicationCode = SamplePublicationCode,
            LanguageId = language.Id,
            Language = language,
            CategoryId = category.Id,
            Category = category,
            CatalogType = CatalogType.Sectioned,
        };
        db.PublicationLanguages.Add(pubLang);
        await db.SaveChangesAsync();

        db.SectionLanguages.Add(new SectionLanguage
        {
            PublicationCode = SamplePublicationCode,
            SectionCode = SampleSectionCode,
            LanguageId = language.Id,
            Language = language,
            PublicationLanguageId = pubLang.Id,
            PublicationLanguage = pubLang,
        });
        await db.SaveChangesAsync();
    }

    internal static async Task SeedFrenchPublicationWithTrackAsync(string mediaIndexPath)
    {
        var options = CreateMediaOptions(mediaIndexPath);
        await using var db = new MediaDbContext(options);

        var language = await db.Languages.SingleAsync(l => l.LanguageCode == FrenchLanguageCode);
        var category = await db.Categories.SingleAsync(c =>
            c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

        var publication = new BiblePublication
        {
            Name = "NWT French",
            PublicationCode = SamplePublicationCode,
            LanguageId = language.Id,
            Language = language,
            IsVideo = false,
            IsMusic = false,
            CatalogType = CatalogType.Sectioned,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { CategoryId = category.Id, Category = category },
            ],
        };
        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync();

        var section = new BiblePublicationSection
        {
            Name = "Genesis",
            SectionCode = SampleSectionCode,
            BiblePublicationId = publication.Id,
            BiblePublication = publication,
        };
        publication.Sections.Add(section);
        await db.SaveChangesAsync();

        var track = new BiblePublicationTrack
        {
            TrackCode = SampleTrackCode,
            Title = "Genesis 1",
            BiblePublicationId = publication.Id,
            Publication = publication,
            BiblePublicationSectionId = section.Id,
            Section = section,
        };
        db.BiblePublicationTracks.Add(track);
        await db.SaveChangesAsync();

        db.TrackUrls.Add(new TrackUrl
        {
            Url = SampleTrackUrl,
            BiblePublicationTrackId = track.Id,
            BiblePublicationTrack = track,
        });
        await db.SaveChangesAsync();
    }

    internal static async Task SeedOldFrenchNwtGenesisTrackAsync(string oldMediaIndexPath)
    {
        var options = CreateMediaOptions(oldMediaIndexPath);
        await using var db = new MediaDbContext(options);

        var category = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
        var language = new Language
        {
            LanguageCode = FrenchLanguageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Categories.Add(category);
        db.Languages.Add(language);
        await db.SaveChangesAsync();

        var publication = new BiblePublication
        {
            Name = "NWT French",
            PublicationCode = SamplePublicationCode,
            LanguageId = language.Id,
            Language = language,
            IsVideo = false,
            IsMusic = false,
            CatalogType = CatalogType.Sectioned,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { CategoryId = category.Id, Category = category },
            ],
        };
        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync();

        var section = new BiblePublicationSection
        {
            Name = "Genesis",
            SectionCode = SampleSectionCode,
            BiblePublicationId = publication.Id,
            BiblePublication = publication,
        };
        publication.Sections.Add(section);
        await db.SaveChangesAsync();

        var track = new BiblePublicationTrack
        {
            TrackCode = SampleTrackCode,
            Title = "Genesis 1",
            BiblePublicationId = publication.Id,
            Publication = publication,
            BiblePublicationSectionId = section.Id,
            Section = section,
        };
        db.BiblePublicationTracks.Add(track);
        await db.SaveChangesAsync();

        db.TrackUrls.Add(new TrackUrl
        {
            Url = SampleTrackUrl,
            BiblePublicationTrackId = track.Id,
            BiblePublicationTrack = track,
        });
        await db.SaveChangesAsync();
    }

    internal static async Task<int> CountTracksAsync(string mediaIndexPath)
    {
        var options = CreateMediaOptions(mediaIndexPath);
        await using var db = new MediaDbContext(options);
        return await db.BiblePublicationTracks.CountAsync();
    }

    internal static async Task<string?> FindTrackUrlAsync(string mediaIndexPath, string trackCode)
    {
        var options = CreateMediaOptions(mediaIndexPath);
        await using var db = new MediaDbContext(options);
        var track = await db.BiblePublicationTracks
            .Include(t => t.TrackUrl)
            .FirstOrDefaultAsync(t => t.TrackCode == trackCode);
        return track?.TrackUrl?.Url;
    }

    internal static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static DbContextOptions<MediaDbContext> CreateMediaOptions(string path) =>
        new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite($"Data Source={path}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
}
