#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;

namespace Bible.Alarm.Tests;

public sealed class PlaybackSliceRecordsEqualityTests
{
    [Fact]
    public void Transport_media_and_default_slices_support_structural_equality()
    {
        var transport = new PlaybackTransportSlice(3, true, false, true, PlayStatus.Playing, false, true);
        Assert.Equal(transport, transport with { });

        var media = new PlaybackMediaSlice("t", "a", "al", "u", TimeSpan.FromSeconds(90), null);
        Assert.Equal(media, media with { });

        var defaults = new PlaybackDefaultScheduleSlice(4, "x", "y", "z", "art");
        Assert.Equal(defaults, defaults with { });
    }
}
