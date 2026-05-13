#nullable enable

using AutoMapper;
using Bible.Alarm.Stores.Mapping;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMappingProfileTests
{
    [Fact]
    public void Profile_registers_with_mapper_configuration()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);

        var mapper = cfg.CreateMapper();
        Assert.NotNull(mapper);
    }
}
