#nullable enable

using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaElementManagerTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new MediaElementManager(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!,
            null!);
        Assert.NotNull(sut);
    }
}
