#nullable enable

using Microsoft.Maui.Devices;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Chooses spinner dimensions so iOS meets a minimum tap target while other platforms inherit standard icon metrics.
/// </summary>
internal static class FontServiceSpinnerDimensionResolver
{
    internal static (double ContainerSize, double FontSize) Resolve(
        DevicePlatform platform,
        double iconStandardContainerSize,
        double iconStandardFontSize)
    {
        if (platform == DevicePlatform.iOS)
        {
            return (Math.Max(56.0, iconStandardContainerSize), Math.Max(24.0, iconStandardFontSize));
        }

        return (iconStandardContainerSize, iconStandardFontSize);
    }
}
