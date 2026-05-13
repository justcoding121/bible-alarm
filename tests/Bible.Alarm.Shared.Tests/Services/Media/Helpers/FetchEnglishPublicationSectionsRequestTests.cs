#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchEnglishPublicationSectionsRequestTests
{
    [Fact]
    public async Task Same_values_compare_equal_including_shared_list_reference()
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
            LanguageCode = "FEPSECL1",
            Direction = Bible.Alarm.Shared.Constants.AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(language);
        await db.SaveChangesAsync();

        var sectionCodes = new List<string> { "40", "41" };

        var a = new FetchEnglishPublicationSectionsRequest(
            db,
            "nwt",
            "E",
            language,
            "Bible",
            IsVideo: false,
            sectionCodes,
            CancellationToken.None);

        var b = new FetchEnglishPublicationSectionsRequest(
            db,
            "nwt",
            "E",
            language,
            "Bible",
            IsVideo: false,
            sectionCodes,
            CancellationToken.None);

        Assert.Equal(a, b);
    }
}
