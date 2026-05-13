#nullable enable

using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class RuntimePlatformFontDefaultsResolverTests
{
    [Fact]
    public void Resolve_returns_iOS_bundle_for_iOS_platform()
    {
        Assert.Same(PlatformFontDefaults.iOS, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.iOS));
    }

    [Fact]
    public void Resolve_returns_Android_bundle_for_Android_platform()
    {
        Assert.Same(PlatformFontDefaults.Android, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.Android));
    }

    [Fact]
    public void Resolve_returns_Windows_bundle_for_WinUI_platform()
    {
        Assert.Same(PlatformFontDefaults.Windows, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.WinUI));
    }

    [Fact]
    public void Resolve_returns_Android_bundle_when_platform_has_no_explicit_other_bundle_mapping()
    {
        Assert.Same(PlatformFontDefaults.Android, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.MacCatalyst));
    }
}
