#nullable enable

using Bible.Alarm.Shared.Database;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_Returns_SqlBacked_MediaDbContext()
    {
        var factory = new MediaDbContextFactory();
        using var db = factory.CreateDbContext([]);
        Assert.IsType<MediaDbContext>(db);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
    }
}
