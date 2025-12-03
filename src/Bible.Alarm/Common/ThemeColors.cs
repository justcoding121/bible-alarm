using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

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
    
    // Day/Calendar Colors (used in converters)
    public static class Day
    {
        public static Color EnabledText => Colors.White;
        public static Color DisabledText => Color.FromArgb("#666666");
        public static Color EnabledBackground => Color.FromArgb("#6A5ACD"); // SlateBlue
        public static Color DisabledBackground => Color.FromArgb("#C0C0C0");
        public static Color DefaultBackground => Color.FromArgb("#D0D0D0");
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
    public static AppTheme GetCurrentTheme()
    {
        return Application.Current?.RequestedTheme ?? AppTheme.Light;
    }
}

