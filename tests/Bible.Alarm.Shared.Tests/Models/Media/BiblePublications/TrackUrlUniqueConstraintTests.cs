#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlUniqueConstraintTests
{
    [Fact]
    public void SaveChanges_second_track_url_for_same_track_fails_one_to_one_constraint()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        int trackId;

        using (var db = new MediaDbContext(options))
        {
            db.Database.EnsureCreated();

            var language = new Language
            {
                LanguageCode = "TU1",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };
            var category = new Category { CategoryCode = "TUCat" };
            db.Languages.Add(language);
            db.Categories.Add(category);
            db.SaveChanges();

            var publication = new BiblePublication
            {
                Name = "Url Pub",
                PublicationCode = "tu-pub",
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
                SectionCode = "2",
                BiblePublication = publication,
            };
            publication.Sections.Add(section);

            var track = new BiblePublicationTrack
            {
                TrackCode = "1",
                Title = "T",
                BiblePublicationId = publication.Id,
                Publication = publication,
                BiblePublicationSectionId = section.Id,
                Section = section,
            };
            section.Tracks.Add(track);

            db.BiblePublications.Add(publication);
            db.SaveChanges();

            trackId = track.Id;

            db.TrackUrls.Add(new TrackUrl
            {
                BiblePublicationTrackId = trackId,
                BiblePublicationTrack = track,
                Url = "https://cdn.example/a.mp3",
            });
            db.SaveChanges();
        }

        using (var db2 = new MediaDbContext(options))
        {
            db2.TrackUrls.Add(new TrackUrl
            {
                BiblePublicationTrackId = trackId,
                Url = "https://cdn.example/b.mp3",
            });

            Assert.Throws<DbUpdateException>(() => db2.SaveChanges());
        }
    }
}
