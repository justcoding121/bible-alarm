#nullable enable

using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DefaultScheduleServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        DefaultScheduleService sut = null!;
        try
        {
            sut = new DefaultScheduleService(
                TestLogging.CreateLogger(),
                null!,
                null!,
                null!,
                null!,
                null!);

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
