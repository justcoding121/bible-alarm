#nullable enable

namespace Bible.Alarm.Services.UI;

internal static class PhoneLayoutWidthClassification
{
    internal static bool IsCompactPhoneWidth(double widthDp, double compactThresholdDp = 600.0)
    {
        return widthDp < compactThresholdDp;
    }
}
