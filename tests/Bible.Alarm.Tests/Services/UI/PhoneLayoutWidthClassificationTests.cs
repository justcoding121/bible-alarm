#nullable enable

using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

public sealed class PhoneLayoutWidthClassificationTests
{
    [Theory]
    [InlineData(599.0, true)]
    [InlineData(600.0, false)]
    public void IsCompactPhoneWidth_treats_default_breakpoint_as_exclusive_upper_bound(double widthDp, bool expectedCompact)
    {
        Assert.Equal(expectedCompact, PhoneLayoutWidthClassification.IsCompactPhoneWidth(widthDp));
    }
}
