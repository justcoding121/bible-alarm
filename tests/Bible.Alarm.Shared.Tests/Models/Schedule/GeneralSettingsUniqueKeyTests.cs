#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsUniqueKeyTests
{
    [Fact]
    public async Task SaveChanges_second_general_settings_row_with_duplicate_Key_fails_unique_index()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new ScheduleDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var db = new ScheduleDbContext(options))
        {
            db.GeneralSettings.Add(new GeneralSettings { Key = "unique.settings.key.1", Value = "a" });
            await db.SaveChangesAsync();

            db.GeneralSettings.Add(new GeneralSettings { Key = "unique.settings.key.1", Value = "b" });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
