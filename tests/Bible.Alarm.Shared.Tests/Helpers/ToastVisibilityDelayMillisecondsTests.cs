#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ToastVisibilityDelayMillisecondsTests
{
    [Fact]
    public void FromNonNegativeUiMilliseconds_clamps_negative_values_to_zero()
    {
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromNonNegativeUiMilliseconds(-5));
    }

    [Fact]
    public void FromToastDurationSeconds_returns_zero_for_non_positive_or_non_finite_inputs()
    {
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(-1));
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(double.NaN));
        Assert.Equal(0, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(double.PositiveInfinity));
    }

    [Fact]
    public void FromToastDurationSeconds_caps_scaled_milliseconds_at_int_max_value()
    {
        Assert.Equal(int.MaxValue, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds((double)int.MaxValue));
    }

    [Fact]
    public void FromToastDurationSeconds_truncates_scaled_fractional_seconds_toward_zero()
    {
        Assert.Equal(1500, ToastVisibilityDelayMilliseconds.FromToastDurationSeconds(1.5));
    }
}
