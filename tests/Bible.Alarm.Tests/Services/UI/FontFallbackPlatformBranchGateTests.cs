#nullable enable

using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class FontFallbackPlatformBranchGateTests
{
    [Fact]
    public void UsesWindowsDesktopFallbackSizing_is_true_for_WinUI_even_when_idiom_not_desktop()
    {
        Assert.True(FontFallbackPlatformBranchGate.UsesWindowsDesktopFallbackSizing(DevicePlatform.WinUI, DeviceIdiom.Phone));
    }

    [Fact]
    public void UsesWindowsDesktopFallbackSizing_is_true_when_idiom_desktop_even_on_Android()
    {
        Assert.True(FontFallbackPlatformBranchGate.UsesWindowsDesktopFallbackSizing(DevicePlatform.Android, DeviceIdiom.Desktop));
    }

    [Fact]
    public void UsesWindowsDesktopFallbackSizing_is_false_for_typical_handheld_Android_phone_profile()
    {
        Assert.False(FontFallbackPlatformBranchGate.UsesWindowsDesktopFallbackSizing(DevicePlatform.Android, DeviceIdiom.Phone));
    }
}
