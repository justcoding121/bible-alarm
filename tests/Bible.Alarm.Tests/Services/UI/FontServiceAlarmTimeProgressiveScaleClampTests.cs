#nullable enable

using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

public sealed class FontServiceAlarmTimeProgressiveScaleClampTests
{
    [Fact]
    public void Apply_returns_original_progressive_scale_when_alarm_base_size_not_above_twenty_points()
    {
        Assert.Equal(
            1.08,
            FontServiceAlarmTimeProgressiveScaleClamp.Apply(18.0, 2.0, 1.08));
    }

    [Fact]
    public void Apply_returns_original_progressive_scale_when_accessibility_scale_not_boosted()
    {
        Assert.Equal(
            1.08,
            FontServiceAlarmTimeProgressiveScaleClamp.Apply(44.0, 1.0, 1.08));
    }

    [Fact]
    public void Apply_raises_small_progressive_scale_to_match_headline_reference_when_alarm_font_is_large()
    {
        var headline = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(20.0, accessibilityScale: 2.0);

        Assert.Equal(
            headline,
            FontServiceAlarmTimeProgressiveScaleClamp.Apply(44.0, 2.0, alarmTimeProgressiveScale: 0.05));
    }

    [Fact]
    public void Apply_caps_progressive_scale_at_ten_percent_above_headline_reference_when_alarm_font_is_large()
    {
        var headline = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(20.0, accessibilityScale: 2.0);

        Assert.Equal(
            headline * 1.1,
            FontServiceAlarmTimeProgressiveScaleClamp.Apply(44.0, 2.0, alarmTimeProgressiveScale: 50.0));
    }
}
