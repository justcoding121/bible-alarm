#nullable enable

using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleContainerServiceTests
{
    [Fact]
    public void Ctor_remembers_logger_dependency()
    {
        var sut = new ScheduleContainerService(TestLogging.CreateLogger());
        Assert.NotNull(sut);
    }
}
