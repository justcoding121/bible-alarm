#nullable enable

using Microsoft.Maui.Devices;

namespace Bible.Alarm.Services.UI;

internal static class FontFallbackPlatformBranchGate
{
    internal static bool UsesWindowsDesktopFallbackSizing(DevicePlatform platform, DeviceIdiom idiom)
    {
        return platform == DevicePlatform.WinUI || idiom == DeviceIdiom.Desktop;
    }
}
