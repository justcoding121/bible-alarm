#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchEnglishPublicationTracksRequestTests
{
    [Fact]
    public async Task Same_values_compare_equal_for_flat_track_seed_request()
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

        var language = new Language
        {
            LanguageCode = "FEPTRK1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var category = new Category { CategoryCode = "FEPTRKC" };
        db.Languages.Add(language);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var a = new FetchEnglishPublicationTracksRequest(
            db,
            "dramas-gn",
            "E",
            language,
            category,
            CategoryName: "Drama",
            IsVideo: true,
            CancellationToken.None);

        var b = new FetchEnglishPublicationTracksRequest(
            db,
            "dramas-gn",
            "E",
            language,
            category,
            CategoryName: "Drama",
            IsVideo: true,
            CancellationToken.None);

        Assert.Equal(a, b);
    }
}
