namespace Bible.Alarm.Common;

/// <summary>
/// Centralized theme-aware color constants for the entire application.
/// All hardcoded Color.FromArgb values should reference these constants.
/// </summary>
public static class ThemeColors
{
    // Background Colors
    public static class Background
    {
        public static Color Dark => Color.FromArgb("#121212");
        public static Color Light => Color.FromArgb("#F8F9FA");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class PageBackground
    {
        public static Color Dark => Color.FromArgb("#121212");
        public static Color Light => Color.FromArgb("#F5F5F5");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class CardBackground
    {
        public static Color Dark => Color.FromArgb("#1E1E1E");
        public static Color Light => Colors.White;

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class ControlBackground
    {
        public static Color Dark => Color.FromArgb("#2A2A2A");
        public static Color Light => Color.FromArgb("#F5F5F5");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class SelectedItemBackground
    {
        public static Color Dark => Color.FromArgb("#3A3A3A");
        public static Color Light => Color.FromArgb("#E8E8E8");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    // Text Colors
    public static class TextPrimary
    {
        public static Color Dark => Colors.White;
        public static Color Light => Color.FromArgb("#212529");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class TextSecondary
    {
        public static Color Dark => Color.FromArgb("#B0B0B0");
        public static Color Light => Color.FromArgb("#6C757D");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class DisabledText
    {
        public static Color Dark => Color.FromArgb("#303030");
        public static Color Light => Color.FromArgb("#B0B0B0");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    // UI Element Colors
    public static class Divider
    {
        public static Color Dark => Color.FromArgb("#404040");
        public static Color Light => Color.FromArgb("#E9ECEF");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class ProgressBarBackground
    {
        public static Color Dark => Color.FromArgb("#404040");
        public static Color Light => Color.FromArgb("#E9ECEF");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    // Primary/Accent Colors
    public static class PrimaryText
    {
        public static Color Dark => Color.FromArgb("#9370DB");
        public static Color Light => Color.FromArgb("#6A5ACD");

        public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
    }

    public static class Primary
    {
        public static Color SlateBlue => Color.FromArgb("#6A5ACD");
        public static Color LightPurple => Color.FromArgb("#9370DB");
        public static Color LightPurpleForDark => Color.FromArgb("#E8E0FF");
    }

    // Day/Calendar Colors (used in converters) - Theme-aware
    public static class Day
    {
        // Text colors
        public static class EnabledText
        {
            public static Color Dark => Colors.White;
            public static Color Light => Colors.White;
            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }

        public static class DisabledText
        {
            // Dark mode: lighter gray for better contrast on dark backgrounds
            public static Color Dark => Color.FromArgb("#B0B0B0");
            // Light mode: darker gray for better contrast on light backgrounds
            public static Color Light => Color.FromArgb("#666666");
            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }

        // Background colors
        public static class EnabledBackground
        {
            // Schedule enabled + Day enabled: Primary color (prominent)
            public static Color Dark => Color.FromArgb("#9370DB"); // Lighter purple for dark mode
            public static Color Light => Color.FromArgb("#6A5ACD"); // SlateBlue for light mode
            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }

        public static class DisabledBackground
        {
            // Schedule enabled + Day disabled: Neutral gray
            public static Color Dark => Color.FromArgb("#404040"); // Darker gray for dark mode
            public static Color Light => Color.FromArgb("#C0C0C0"); // Light gray for light mode
            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }

        public static class DefaultBackground
        {
            // Schedule disabled + Day enabled: Matches add button disabled state (PrimaryColor at 50% opacity)
            // PrimaryColor #6A5ACD (RGB: 106, 90, 205) at 50% opacity over typical backgrounds
            // Approximates to a dimmer slate blue that matches the visual appearance
            public static Color Dark => Color.FromArgb("#554A9F"); // Dimmer slate blue for dark mode (matches disabled add button)
            public static Color Light => Color.FromArgb("#B5A5D5"); // Dimmer slate blue for light mode (matches disabled add button)
            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }

        public static class AlarmDisabledDayDisabledBackground
        {
            // Schedule disabled + Day disabled: More muted gray to distinguish from enabled schedule + disabled day
            public static Color Dark => Color.FromArgb("#2A2A2A"); // Darker, more muted gray for dark mode
            public static Color Light => Color.FromArgb("#E0E0E0"); // Lighter gray for light mode
            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }

        // Legacy properties for backward compatibility (use Light theme as default)
        [Obsolete("Use EnabledText.Get(theme) instead")]
        public static Color EnabledTextLegacy => EnabledText.Light;
        
        [Obsolete("Use DisabledText.Get(theme) instead")]
        public static Color DisabledTextLegacy => DisabledText.Light;
        
        [Obsolete("Use EnabledBackground.Get(theme) instead")]
        public static Color EnabledBackgroundLegacy => EnabledBackground.Light;
        
        [Obsolete("Use DisabledBackground.Get(theme) instead")]
        public static Color DisabledBackgroundLegacy => DisabledBackground.Light;
        
        [Obsolete("Use DefaultBackground.Get(theme) instead")]
        public static Color DefaultBackgroundLegacy => DefaultBackground.Light;
    }

    // Animation/Interaction Colors
    public static class Animation
    {
        public static Color PressedBackground => Color.FromArgb("#E0E0E0");
        public static Color Shadow => Color.FromArgb("#40000000");
    }

    // Bootstrap/Splash Colors
    public static class Bootstrap
    {
        public static Color DarkBackground => Color.FromArgb("#1A1A1A");
        public static Color LightBackground => Colors.White;
    }

    // Fallback colors for converters (when resources aren't available)
    public static class Fallback
    {
        public static class DisabledText
        {
            public static Color Dark => Color.FromArgb("#404040");
            public static Color Light => Color.FromArgb("#B0B0B0");

            public static Color Get(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        }
    }

    /// <summary>
    /// Helper method to get the current theme
    /// </summary>
    public static AppTheme GetCurrentTheme() => Application.Current?.RequestedTheme ?? AppTheme.Light;
}

