#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationCategoryTests
{
    [Fact]
    public void Add_second_junction_row_with_same_publication_and_category_is_rejected_by_tracker()
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
            LanguageCode = "BC1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var category = new Category { CategoryCode = "BCCat" };
        db.Languages.Add(language);
        db.Categories.Add(category);
        db.SaveChanges();

        var publication = new BiblePublication
        {
            Name = "Pub A",
            PublicationCode = "bc-pub",
            LanguageId = language.Id,
            Language = language,
            BiblePublicationCategories = [],
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };
        publication.BiblePublicationCategories.Add(
            new BiblePublicationCategory
            {
                BiblePublication = publication,
                Category = category,
                CategoryId = category.Id,
            });

        db.BiblePublications.Add(publication);
        db.SaveChanges();

        Assert.Throws<InvalidOperationException>(() =>
            db.BiblePublicationCategories.Add(new BiblePublicationCategory
            {
                BiblePublicationId = publication.Id,
                BiblePublication = publication,
                CategoryId = category.Id,
                Category = category,
            }));
    }
}
