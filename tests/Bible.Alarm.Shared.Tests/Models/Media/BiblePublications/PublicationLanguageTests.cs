#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageTests
{
    [Fact]
    public void SaveChanges_second_row_with_same_publication_language_and_category_triplet_fails_unique_index()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var db = new MediaDbContext(options);
        db.Database.EnsureCreated();

        var category = new Category { CategoryCode = "BibleZ" };
        db.Categories.Add(category);
        var language = new Language
        {
            LanguageCode = "Z8",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(language);
        db.SaveChanges();

        db.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = "nwt",
            LanguageId = language.Id,
            CategoryId = category.Id,
            Category = category,
            Language = language,
            SectionLanguages = [],
        });
        db.SaveChanges();

        db.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = "nwt",
            LanguageId = language.Id,
            CategoryId = category.Id,
            Category = category,
            Language = language,
            SectionLanguages = [],
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
