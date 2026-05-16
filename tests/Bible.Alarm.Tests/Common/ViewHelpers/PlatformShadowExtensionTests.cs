#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class PlatformShadowExtensionTests
{
    [Fact]
    public void ProvideValue_matches_platform_shadow_policy()
    {
        var sut = new PlatformShadowExtension
        {
            Brush = "#FF001122",
            Offset = "2,4",
            Radius = "6",
            Opacity = "0.5",
        };

        var shadow = sut.ProvideValue(StubServiceProvider.Instance);

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            Assert.Null(shadow);
        }
        else
        {
            Assert.NotNull(shadow);
            Assert.Equal(6f, shadow!.Radius);
            Assert.Equal(0.5f, shadow.Opacity);
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public static readonly StubServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
