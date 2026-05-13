#nullable enable

using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class FallbackFontDeviceCategoryResolverTests
{
    [Fact]
    public void Resolve_maps_desktop_idiom_to_desktop_category()
    {
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Desktop,
            FallbackFontDeviceCategoryResolver.Resolve(DeviceIdiom.Desktop));
    }

    [Fact]
    public void Resolve_maps_non_desktop_idiom_to_phone_category()
    {
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Phone,
            FallbackFontDeviceCategoryResolver.Resolve(DeviceIdiom.Phone));
    }
}
