#nullable enable

using Bible.Alarm.Shared.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests.Support;

internal static class MediaFetcherRequestTestDb
{
    public static async Task RunAsync(Func<MediaDbContext, Task> body)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await body(db);
    }
}
