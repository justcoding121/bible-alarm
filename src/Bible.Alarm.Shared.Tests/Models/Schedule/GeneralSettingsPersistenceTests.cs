#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsPersistenceTests
{
    private static async Task<DbContextOptions<ScheduleDbContext>> BuildOptionsAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new ScheduleDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        return options;
    }

    [Fact]
    public async Task Persist_RoundTrips_Key_And_Nullable_Value()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        await using (var db = new ScheduleDbContext(options))
        {
            db.GeneralSettings.Add(new GeneralSettings { Key = "pref.theme", Value = "dark" });
            await db.SaveChangesAsync();
        }

        await using var read = new ScheduleDbContext(options);
        var row = await read.GeneralSettings.AsNoTracking().SingleAsync(g => g.Key == "pref.theme");

        Assert.Equal("dark", row.Value);
    }

    [Fact]
    public async Task Persist_Enforces_UniqueIndex_On_Key()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        await using var db = new ScheduleDbContext(options);
        db.GeneralSettings.Add(new GeneralSettings { Key = "dup", Value = "a" });
        db.GeneralSettings.Add(new GeneralSettings { Key = "dup", Value = "b" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
