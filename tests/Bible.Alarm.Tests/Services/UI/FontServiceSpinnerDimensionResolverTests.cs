#nullable enable

using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class FontServiceSpinnerDimensionResolverTests
{
    [Fact]
    public void Resolve_returns_minimum_ios_spinner_dimensions_when_platform_is_ios()
    {
        var result = FontServiceSpinnerDimensionResolver.Resolve(DevicePlatform.iOS, 48.0, 22.0);

        Assert.Equal(56.0, result.ContainerSize);
        Assert.Equal(24.0, result.FontSize);
    }

    [Fact]
    public void Resolve_returns_icon_sizes_unchanged_when_platform_is_win_ui()
    {
        var result = FontServiceSpinnerDimensionResolver.Resolve(DevicePlatform.WinUI, 48.0, 22.0);

        Assert.Equal(48.0, result.ContainerSize);
        Assert.Equal(22.0, result.FontSize);
    }
}
