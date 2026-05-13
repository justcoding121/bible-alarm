#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaElementServiceTests
{
    [Fact]
    public void Ctor_accepts_logger()
    {
        var sut = new MediaElementService(TestLogging.CreateLogger());
        Assert.NotNull(sut);
    }
}
