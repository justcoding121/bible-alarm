#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackUniqueConstraintTests
{
    [Fact]
    public void SaveChanges_second_track_in_section_with_same_TrackCode_fails_unique_index()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var db = new MediaDbContext(options);
        db.Database.EnsureCreated();

        var language = new Language
        {
            LanguageCode = "BT1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var category = new Category { CategoryCode = "BTCat" };
        db.Languages.Add(language);
        db.Categories.Add(category);
        db.SaveChanges();

        var publication = new BiblePublication
        {
            Name = "Tr Pub",
            PublicationCode = "tr-pub",
            LanguageId = language.Id,
            Language = language,
            BiblePublicationCategories = [],
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };
        publication.BiblePublicationCategories.Add(new BiblePublicationCategory
        {
            BiblePublication = publication,
            Category = category,
            CategoryId = category.Id,
        });

        var section = new BiblePublicationSection
        {
            Name = "Sec",
            SectionCode = "1",
            BiblePublication = publication,
        };
        publication.Sections.Add(section);

        db.BiblePublications.Add(publication);
        db.SaveChanges();

        var track1 = new BiblePublicationTrack
        {
            TrackCode = "9",
            Title = "T1",
            BiblePublicationId = publication.Id,
            Publication = publication,
            BiblePublicationSectionId = section.Id,
            Section = section,
        };
        section.Tracks.Add(track1);
        db.BiblePublicationTracks.Add(track1);
        db.SaveChanges();

        var track2 = new BiblePublicationTrack
        {
            TrackCode = "9",
            Title = "T2",
            BiblePublicationId = publication.Id,
            Publication = publication,
            BiblePublicationSectionId = section.Id,
            Section = section,
        };
        section.Tracks.Add(track2);
        db.BiblePublicationTracks.Add(track2);

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
