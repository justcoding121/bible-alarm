#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class NegateBooleanConverterTests
{
    [Fact]
    public void Convert_throws_InvalidCastException_when_value_is_not_bool()
    {
        var sut = new NegateBooleanConverter();

        Assert.Throws<InvalidCastException>(() =>
            sut.Convert("not-a-bool", typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_throws_InvalidCastException_when_target_is_not_bool()
    {
        var sut = new NegateBooleanConverter();

        Assert.Throws<InvalidCastException>(() =>
            sut.ConvertBack("x", typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
