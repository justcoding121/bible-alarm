#nullable enable

namespace Bible.Alarm.Common.ViewHelpers;

internal static class MiniPlaybackBarMeasurementRecorder
{
    internal static bool TryRecordVisibleHeight(bool isVisible, double height, ref double lastMeasuredHeight)
    {
        if (!isVisible || height <= 0)
        {
            return false;
        }

        lastMeasuredHeight = height;
        return true;
    }
}
