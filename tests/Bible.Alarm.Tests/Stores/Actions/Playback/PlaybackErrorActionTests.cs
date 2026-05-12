#nullable enable

using Bible.Alarm.Stores.Actions.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackErrorActionTests
{
    [Fact]
    public void Init_sets_ErrorMessage()
    {
        var sut = new PlaybackErrorAction { ErrorMessage = "err" };

        Assert.Equal("err", sut.ErrorMessage);
    }
}
