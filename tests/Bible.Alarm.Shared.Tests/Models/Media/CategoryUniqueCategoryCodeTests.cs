#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryUniqueCategoryCodeTests
{
    [Fact]
    public void SaveChanges_second_category_with_duplicate_CategoryCode_fails_unique_index()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var db = new MediaDbContext(options);
        db.Database.EnsureCreated();

        db.Categories.Add(new Category { CategoryCode = "CatUQ1" });
        db.SaveChanges();

        db.Categories.Add(new Category { CategoryCode = "CatUQ1" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
