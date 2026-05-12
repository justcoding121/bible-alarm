#nullable enable

using Bible.Alarm.Services.Media.Models;

namespace Bible.Alarm.Tests;

public sealed class PlayStatusTests
{
    [Fact]
    public void Enum_defines_expected_playback_lifecycle_values()
    {
        var values = Enum.GetValues<PlayStatus>();

        Assert.Equal(6, values.Length);
        Assert.Contains(PlayStatus.Stopped, values);
        Assert.Contains(PlayStatus.Playing, values);
        Assert.Contains(PlayStatus.Ended, values);
    }
}
