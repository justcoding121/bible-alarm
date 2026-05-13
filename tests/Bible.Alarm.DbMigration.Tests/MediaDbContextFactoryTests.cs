#nullable enable

using Bible.Alarm.DbMigration;
using Bible.Alarm.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.DbMigration.Tests;

public sealed class MediaDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_uses_connection_string_containing_media_index_database_file()
    {
        var sut = new MediaDbContextFactory();

        using var context = sut.CreateDbContext([]);
        var connectionString = context.Database.GetDbConnection().ConnectionString;

        Assert.NotNull(connectionString);
        Assert.Contains(AppConstants.Database.MediaIndexDatabaseFileName, connectionString, StringComparison.OrdinalIgnoreCase);
    }
}
