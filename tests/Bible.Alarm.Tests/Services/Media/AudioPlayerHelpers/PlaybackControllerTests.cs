#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class PlaybackControllerTests
{
    [Fact]
    public void Ctor_accepts_collaborators()
    {
        var sut = new PlaybackController(
            TestLogging.CreateLogger(),
            null!,
            () => null);
        Assert.NotNull(sut);
    }
}
