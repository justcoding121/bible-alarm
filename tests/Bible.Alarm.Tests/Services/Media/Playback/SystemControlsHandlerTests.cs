#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class SystemControlsHandlerTests
{
    [Fact]
    public void Ctor_stores_logger()
    {
        var sut = new SystemControlsHandler(TestLogging.CreateLogger());
        Assert.NotNull(sut);
    }
}
