#nullable enable

using Microsoft.Maui.Devices;

namespace Bible.Alarm.Services.UI;

internal static class FallbackFontDeviceCategoryResolver
{
    internal static FontServiceSizingHelpers.DeviceSizeCategory Resolve(DeviceIdiom idiom)
    {
        return idiom == DeviceIdiom.Desktop
            ? FontServiceSizingHelpers.DeviceSizeCategory.Desktop
            : FontServiceSizingHelpers.DeviceSizeCategory.Phone;
    }
}
