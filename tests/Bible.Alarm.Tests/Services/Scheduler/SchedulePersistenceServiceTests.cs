#nullable enable

using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class SchedulePersistenceServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
