#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class MiniPlaybackBarMeasurementRecorderTests
{
    [Fact]
    public void TryRecordVisibleHeight_preserves_last_measurement_when_bar_not_visible()
    {
        double stored = 55;
        Assert.False(MiniPlaybackBarMeasurementRecorder.TryRecordVisibleHeight(isVisible: false, height: 120, ref stored));
        Assert.Equal(55, stored);
    }

    [Fact]
    public void TryRecordVisibleHeight_preserves_last_measurement_when_height_not_positive()
    {
        double stored = 55;
        Assert.False(MiniPlaybackBarMeasurementRecorder.TryRecordVisibleHeight(isVisible: true, height: 0, ref stored));
        Assert.Equal(55, stored);
    }

    [Fact]
    public void TryRecordVisibleHeight_writes_positive_height_when_visible()
    {
        double stored = 1;
        Assert.True(MiniPlaybackBarMeasurementRecorder.TryRecordVisibleHeight(isVisible: true, height: 48, ref stored));
        Assert.Equal(48, stored);
    }
}
