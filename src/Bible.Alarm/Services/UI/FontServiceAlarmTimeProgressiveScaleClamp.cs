#nullable enable

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Keeps very large alarm clock text from scaling far below the progressive scale applied to headline-sized text.
/// </summary>
internal static class FontServiceAlarmTimeProgressiveScaleClamp
{
    internal static double Apply(double baseAlarmTimeSize, double accessibilityScale, double alarmTimeProgressiveScale)
    {
        if (baseAlarmTimeSize <= 20.0 || accessibilityScale <= 1.0)
        {
            return alarmTimeProgressiveScale;
        }

        var headlineProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(20.0, accessibilityScale);
        var boosted = Math.Max(alarmTimeProgressiveScale, headlineProgressiveScale);
        return Math.Min(boosted, headlineProgressiveScale * 1.1);
    }
}
