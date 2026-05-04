namespace Bible.Alarm.DbMigration.Tests;

public class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void ScheduleDbContextFactory_CreateDbContext_ReturnsContext()
    {
        var factory = new ScheduleDbContextFactory();

        using var ctx = factory.CreateDbContext([]);

        Assert.NotNull(ctx);
    }

    [Fact]
    public void MediaDbContextFactory_CreateDbContext_ReturnsContext()
    {
        var factory = new MediaDbContextFactory();

        using var ctx = factory.CreateDbContext([]);

        Assert.NotNull(ctx);
    }
}
