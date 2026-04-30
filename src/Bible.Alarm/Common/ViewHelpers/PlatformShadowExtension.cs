#nullable enable

using System.Globalization;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Returns a <see cref="Shadow"/> on Android/iOS and null on WinUI to avoid
/// GetAlphaMaskAsync ArgumentException crash on Windows.
/// </summary>
[AcceptEmptyServiceProvider]
public sealed class PlatformShadowExtension : IMarkupExtension<Shadow?>
{
    public string? Brush { get; set; }
    public string? LightBrush { get; set; }
    public string? DarkBrush { get; set; }
    public string? Offset { get; set; }
    public string? Radius { get; set; }
    public string? Opacity { get; set; }

    public Shadow? ProvideValue(IServiceProvider serviceProvider)
    {
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return null;
        }

        var shadow = new Shadow();

        if (!string.IsNullOrEmpty(Brush))
        {
            shadow.Brush = new SolidColorBrush(Color.FromArgb(Brush));
        }
        else if (!string.IsNullOrEmpty(LightBrush) && !string.IsNullOrEmpty(DarkBrush))
        {
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            shadow.Brush = new SolidColorBrush(Color.FromArgb(isDark ? DarkBrush : LightBrush));
        }

        if (!string.IsNullOrEmpty(Offset) && ParseOffset(Offset) is { } point)
        {
            shadow.Offset = point;
        }

        if (!string.IsNullOrEmpty(Radius) && float.TryParse(Radius, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius))
        {
            shadow.Radius = radius;
        }

        if (!string.IsNullOrEmpty(Opacity) && float.TryParse(Opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
        {
            shadow.Opacity = opacity;
        }

        return shadow;
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider)!;

    private static Point? ParseOffset(string offset)
    {
        var parts = offset.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        if (float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return new Point(x, y);
        }

        return null;
    }
}
