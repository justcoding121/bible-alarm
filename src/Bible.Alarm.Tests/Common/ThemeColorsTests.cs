#nullable enable

using Bible.Alarm.Common;

namespace Bible.Alarm.Tests;

public sealed class ThemeColorsTests
{
    [Fact]
    public void GetCurrentTheme_defaults_to_light_when_application_missing()
    {
        Assert.Equal(AppTheme.Light, ThemeColors.GetCurrentTheme());
    }

    [Fact]
    public void Theme_nested_Get_selects_dark_or_light_palette()
    {
        Assert.Equal(ThemeColors.Background.Dark, ThemeColors.Background.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.Background.Light, ThemeColors.Background.Get(AppTheme.Light));

        Assert.Equal(ThemeColors.PageBackground.Dark, ThemeColors.PageBackground.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.CardBackground.Light, ThemeColors.CardBackground.Get(AppTheme.Light));

        Assert.Equal(ThemeColors.TextPrimary.Dark, ThemeColors.TextPrimary.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.TextSecondary.Light, ThemeColors.TextSecondary.Get(AppTheme.Light));

        Assert.Equal(ThemeColors.Divider.Dark, ThemeColors.Divider.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.ProgressBarBackground.Light, ThemeColors.ProgressBarBackground.Get(AppTheme.Light));

        Assert.Equal(ThemeColors.PrimaryText.Dark, ThemeColors.PrimaryText.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.DisabledText.Light, ThemeColors.DisabledText.Get(AppTheme.Light));

        Assert.Equal(ThemeColors.Fallback.ConverterMutedText.Dark, ThemeColors.Fallback.ConverterMutedText.Get(AppTheme.Dark));
    }

    [Fact]
    public void Day_calendar_colors_track_theme()
    {
        Assert.Equal(ThemeColors.Day.EnabledText.Dark, ThemeColors.Day.EnabledText.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.Day.CalendarMutedText.Light, ThemeColors.Day.CalendarMutedText.Get(AppTheme.Light));
        Assert.Equal(ThemeColors.Day.EnabledBackground.Dark, ThemeColors.Day.EnabledBackground.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.Day.DisabledBackground.Light, ThemeColors.Day.DisabledBackground.Get(AppTheme.Light));
        Assert.Equal(ThemeColors.Day.DefaultBackground.Dark, ThemeColors.Day.DefaultBackground.Get(AppTheme.Dark));
        Assert.Equal(ThemeColors.Day.AlarmDisabledDayDisabledBackground.Light,
            ThemeColors.Day.AlarmDisabledDayDisabledBackground.Get(AppTheme.Light));
    }

    [Fact]
    public void Primary_constants_are_non_default_colors()
    {
        Assert.NotEqual(Colors.Transparent, ThemeColors.Primary.SlateBlue);
        Assert.NotEqual(Colors.Transparent, ThemeColors.Primary.LightPurple);
        Assert.NotEqual(Colors.Transparent, ThemeColors.Animation.Shadow);
        Assert.NotEqual(Colors.Transparent, ThemeColors.Bootstrap.DarkBackground);
    }
}
