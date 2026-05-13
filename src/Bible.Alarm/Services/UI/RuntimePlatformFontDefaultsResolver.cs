#nullable enable

using Microsoft.Maui.Devices;

namespace Bible.Alarm.Services.UI;

internal static class RuntimePlatformFontDefaultsResolver
{
    internal static PlatformFontDefaults Resolve(DevicePlatform platform)
    {
        if (platform == DevicePlatform.iOS)
        {
            return PlatformFontDefaults.iOS;
        }

        if (platform == DevicePlatform.Android)
        {
            return PlatformFontDefaults.Android;
        }

        if (platform == DevicePlatform.WinUI)
        {
            return PlatformFontDefaults.Windows;
        }

        return PlatformFontDefaults.Android;
    }
}
