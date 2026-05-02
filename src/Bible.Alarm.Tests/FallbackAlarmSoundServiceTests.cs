#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class FallbackAlarmSoundServiceTests
{
    [Fact]
    public async Task GetFallbackAlarmTrackAsync_returns_null_when_platform_default_uri_unavailable()
    {
        var logger = TestLogging.CreateLogger();
        var sut = new FallbackAlarmSoundService(logger);

        var track = await sut.GetFallbackAlarmTrackAsync();

        Assert.Null(track);
    }
}
