#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlPersistenceTests
{
    private static async Task<DbContextOptions<MediaDbContext>> BuildOptionsAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        return options;
    }

    [Fact]
    public async Task Persist_With_NullTrack_Fk_RoundTrips_Url()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        const string url = "https://cdn.example/standalone.mp3";

        await using (var db = new MediaDbContext(options))
        {
            db.TrackUrls.Add(new TrackUrl { Url = url });
            await db.SaveChangesAsync();
        }

        await using var read = new MediaDbContext(options);
        var row = await read.TrackUrls.AsNoTracking().SingleAsync();

        Assert.Equal(url, row.Url);
        Assert.Null(row.BiblePublicationTrackId);
    }

    [Fact]
    public async Task Persist_Linked_To_Track_Reloads_Navigation_With_Include()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        await using (var db = new MediaDbContext(options))
        {
            var language = new Language
            {
                LanguageCode = "TU-LINK-E1",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };
            db.Languages.Add(language);
            await db.SaveChangesAsync();

            var publication = new BiblePublication
            {
                Name = "Track Url Pub",
                PublicationCode = "pub-trackurl-linked",
                LanguageId = language.Id,
                Language = language,
                IsVideo = false,
                IsMusic = false,
            };

            var section = new BiblePublicationSection
            {
                Name = "S1",
                SectionCode = "mat-1",
                BiblePublication = publication,
            };
            publication.Sections.Add(section);

            var track = new BiblePublicationTrack
            {
                TrackCode = "42",
                Title = "Chapter",
                Publication = publication,
                Section = section,
            };
            section.Tracks.Add(track);

            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync();

            db.TrackUrls.Add(new TrackUrl
            {
                Url = "https://cdn.linked/track.mp3",
                BiblePublicationTrack = track,
            });
            await db.SaveChangesAsync();
        }

        await using var read = new MediaDbContext(options);
        var row = await read.TrackUrls
            .AsNoTracking()
            .Include(tu => tu.BiblePublicationTrack)
            .SingleAsync();

        Assert.Equal("https://cdn.linked/track.mp3", row.Url);
        Assert.NotNull(row.BiblePublicationTrack);
        Assert.Equal("42", row.BiblePublicationTrack!.TrackCode);
    }
}
