#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Shared helper methods for <see cref="FontService"/> to keep the service file small.
/// </summary>
internal static class FontServiceSizingHelpers
{
    /// <summary>
    /// Device size categories based on screen width in density-independent pixels
    /// Following industry standards: phones less than 600dp, tablets 600-960dp, desktop greater than 960dp
    /// </summary>
    internal enum DeviceSizeCategory
    {
        Phone,      // < 600dp (small to large phones)
        Tablet,     // 600-960dp (tablets, foldables)
        Desktop     // > 960dp (desktop, large tablets)
    }

    internal static DeviceSizeCategory GetDeviceSizeCategory(double widthDp, DeviceIdiom idiom)
    {
        // Desktop idiom always uses desktop category
        if (idiom == DeviceIdiom.Desktop)
        {
            return DeviceSizeCategory.Desktop;
        }

        if (widthDp < 600)
        {
            return DeviceSizeCategory.Phone;
        }

        if (widthDp < 960)
        {
            return DeviceSizeCategory.Tablet;
        }

        return DeviceSizeCategory.Desktop;
    }

    /// <summary>
    /// Gets device size multiplier for font scaling per industry standards
    /// - Phones: 1.0x (base size)
    /// - Tablets: 1.15-1.20x (slightly larger for better readability at viewing distance)
    /// - Desktop: 1.0-1.1x (desktop apps typically use consistent sizes)
    /// </summary>
    internal static double GetDeviceSizeMultiplier(DeviceSizeCategory category, DevicePlatform platform)
    {
        if (category == DeviceSizeCategory.Phone)
        {
            return 1.0; // Base size for phones
        }

        if (category == DeviceSizeCategory.Tablet)
        {
            if (platform == DevicePlatform.iOS)
            {
                return 1.15; // iOS tablets: 15% larger (per HIG recommendations)
            }

            if (platform == DevicePlatform.Android)
            {
                return 1.20; // Android tablets: 20% larger (per Material Design)
            }

            return 1.15; // Default for other platforms
        }

        if (category == DeviceSizeCategory.Desktop)
        {
            if (platform == DevicePlatform.WinUI)
            {
                return 1.0; // Windows desktop: standard size
            }

            return 1.1; // Other desktop: slight increase
        }

        return 1.0;
    }

    internal static double CalculateDensityScaleFactor(double widthDp, double density)
    {
        bool isPhone = widthDp < 600;
        bool isTablet = widthDp is >= 600 and < 960;

        if (isPhone)
        {
            return Math.Min(density, 1.5);
        }

        if (isTablet)
        {
            return Math.Min(density, 1.8);
        }

        return Math.Min(density, 2.2);
    }

    /// <summary>
    /// Calculates a progressive accessibility scale factor based on base font size.
    /// Smaller fonts scale more with OS accessibility settings, while larger fonts scale less.
    /// </summary>
    internal static double CalculateProgressiveAccessibilityScale(double baseFontSize, double accessibilityScale)
    {
        // If accessibility scale is 1.0 (normal), no scaling needed
        if (accessibilityScale <= 1.0)
        {
            return 1.0;
        }

        // Calculate how much extra scaling is needed beyond normal
        double extraScale = accessibilityScale - 1.0;

        // Progressive scaling based on font size:
        // - Very small fonts (<=10pt): scale fully (100% of extra scale)
        // - Small fonts (10-12pt): scale fully (100% of extra scale)
        // - Medium fonts (12-16pt): scale moderately (50% of extra scale)
        // - Large fonts (16-20pt): scale minimally (25% of extra scale)
        // - Very large fonts (>20pt): scale very minimally (10% of extra scale)

        double progressiveFactor;
        if (baseFontSize <= 10.0)
        {
            // Very small fonts: full scaling
            progressiveFactor = 1.0;
        }
        else if (baseFontSize <= 12.0)
        {
            // Small fonts: full scaling
            progressiveFactor = 1.0;
        }
        else if (baseFontSize <= 16.0)
        {
            // Medium fonts: moderate scaling (interpolate from 1.0 at 12pt to 0.5 at 16pt)
            progressiveFactor = 1.0 - ((baseFontSize - 12.0) / 4.0) * 0.5;
        }
        else if (baseFontSize <= 20.0)
        {
            // Large fonts: minimal scaling (interpolate from 0.5 at 16pt to 0.25 at 20pt)
            progressiveFactor = 0.5 - ((baseFontSize - 16.0) / 4.0) * 0.25;
        }
        else
        {
            // Very large fonts: very minimal scaling (interpolate from 0.25 at 20pt to 0.1 at 30pt+)
            progressiveFactor = Math.Max(0.1, 0.25 - ((baseFontSize - 20.0) / 10.0) * 0.15);
        }

        // Apply progressive factor to extra scale, then add back the base 1.0
        return 1.0 + (extraScale * progressiveFactor);
    }

    internal static (double MaxTime, double MaxMeridian, double MaxBellIcon) GetAlarmMaxSizes(
        bool isPhone,
        double accessibilityScale,
        DevicePlatform platform,
        double deviceSizeMultiplier)
    {
        // Alarm fonts are already large, so max sizes should scale minimally with accessibility
        // Use a very minimal progressive scale for the max caps (similar to very large fonts)
        double maxProgressiveScale = CalculateProgressiveAccessibilityScale(30.0, accessibilityScale);
        double maxScaleFactor = 1.0 + ((maxProgressiveScale - 1.0) * 0.5); // Further reduce max scaling

        // Get platform-specific base sizes and calculate max as multiples (allows ~2x scaling for accessibility)
        PlatformFontDefaults defaults;
        if (platform == DevicePlatform.iOS)
        {
            defaults = PlatformFontDefaults.iOS;
        }
        else if (platform == DevicePlatform.Android)
        {
            defaults = PlatformFontDefaults.Android;
        }
        else if (platform == DevicePlatform.WinUI)
        {
            defaults = PlatformFontDefaults.Windows;
        }
        else
        {
            defaults = PlatformFontDefaults.Android;
        }

        double baseTimeSize = defaults.AlarmTimeSize * deviceSizeMultiplier;
        double baseMeridianSize = defaults.AlarmMeridianSize * deviceSizeMultiplier;

        // Max sizes: allow up to 2.5x for accessibility, with higher limits for tablets
        // Increased multipliers to allow alarm time to grow larger
        double maxTimeMultiplier = isPhone ? 2.5 : 3.0;
        double maxMeridianMultiplier = isPhone ? 2.0 : 2.5;

        double maxAlarmTimeSize = baseTimeSize * maxTimeMultiplier * maxScaleFactor;
        double maxAlarmMeridianSize = baseMeridianSize * maxMeridianMultiplier * maxScaleFactor;
        double maxAlarmBellIconSize = (isPhone ? 100.0 : 130.0) * maxScaleFactor;

        return (maxAlarmTimeSize, maxAlarmMeridianSize, maxAlarmBellIconSize);
    }
}

