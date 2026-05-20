#nullable enable

using Bible.Alarm.Shared.Database;

namespace Bible.Alarm.Tests;

public sealed class MediaDbContextBibleAlarmTests
{
    [Fact]
    public void Parameterless_ctor_can_be_constructed()
    {
        using var db = new MediaDbContext();
        Assert.NotNull(db);
    }
}
