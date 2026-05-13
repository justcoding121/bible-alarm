#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Bootstrap;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStatePopulatorTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        var deps = new ScheduleStatePopulatorDeps(
            null,
            null,
            mapper,
            null,
            null,
            null,
            null!);

        var sut = new ScheduleStatePopulator(deps);

        Assert.NotNull(sut);
    }
}
