#nullable enable

using Bible.Alarm.DbMigration;
using Bible.Alarm.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.DbMigration.Tests;

public sealed class ScheduleDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_uses_connection_string_containing_schedule_database_file()
    {
        var sut = new ScheduleDbContextFactory();

        using var context = sut.CreateDbContext([]);
        var connectionString = context.Database.GetDbConnection().ConnectionString;

        Assert.NotNull(connectionString);
        Assert.Contains(AppConstants.Database.ScheduleDatabaseFileName, connectionString, StringComparison.OrdinalIgnoreCase);
    }
}
