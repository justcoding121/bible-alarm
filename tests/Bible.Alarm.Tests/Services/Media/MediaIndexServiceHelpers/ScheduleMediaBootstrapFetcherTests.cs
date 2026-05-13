#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMediaBootstrapFetcherTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new ScheduleMediaBootstrapFetcher(
            TestLogging.CreateLogger(),
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
