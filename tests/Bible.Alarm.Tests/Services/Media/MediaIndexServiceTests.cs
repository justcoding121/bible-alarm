#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaIndexServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new MediaIndexService(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
