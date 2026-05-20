#nullable enable

using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class RuntimePlatformFontDefaultsResolverTests
{
    [Fact]
    public void Resolve_returns_platform_specific_defaults()
    {
        Assert.Same(PlatformFontDefaults.iOS, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.iOS));
        Assert.Same(PlatformFontDefaults.Android, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.Android));
        Assert.Same(PlatformFontDefaults.Windows, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.WinUI));
        Assert.Same(PlatformFontDefaults.Android, RuntimePlatformFontDefaultsResolver.Resolve(DevicePlatform.MacCatalyst));
    }
}
