#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;

namespace Bible.Alarm.Tests;

public sealed class PlaybackStateTests
{
    [Fact]
    public void Constructor_maps_IsTransitioningTrack_from_transport_slice()
    {
        var transport = new PlaybackTransportSlice(
            CurrentScheduleId: null,
            IsPreparingOrPlaying: false,
            CanPlayNext: false,
            CanPlayPrevious: false,
            Status: PlayStatus.Stopped,
            IsAutoAdvancing: false,
            IsTransitioningTrack: true);
        var media = new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null);
        var defaults = new PlaybackDefaultScheduleSlice(null, null, null, null, null);

        var sut = new PlaybackState(transport, media, defaults);

        Assert.True(sut.IsTransitioningTrack);
    }
}
