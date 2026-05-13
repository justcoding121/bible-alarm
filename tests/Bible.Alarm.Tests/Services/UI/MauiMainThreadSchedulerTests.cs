#nullable enable

using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

public sealed class MauiMainThreadSchedulerTests
{
    [Fact]
    public void Ctor_builds_scheduler()
    {
        var sut = new MauiMainThreadScheduler();
        Assert.NotNull(sut);
    }
}
