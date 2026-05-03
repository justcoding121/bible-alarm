#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;

namespace Bible.Alarm.Tests;

public sealed class TimeToMeridianConverterTests
{
    [Fact]
    public void Convert_TimeSpan_uses_current_culture_designator()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var sut = new TimeToMeridianConverter();

            Assert.Equal("PM", sut.Convert(new TimeSpan(15, 0, 0), typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal("AM", sut.Convert(new TimeSpan(3, 0, 0), typeof(string), null, CultureInfo.InvariantCulture));
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public void Convert_non_TimeSpan_uses_today_designator()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var sut = new TimeToMeridianConverter();

            var result = Assert.IsType<string>(sut.Convert(new object(), typeof(string), null, CultureInfo.InvariantCulture));

            Assert.True(result is "AM" or "PM");
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new TimeToMeridianConverter();

        Assert.Throws<NotSupportedException>(() =>
            sut.ConvertBack("PM", typeof(TimeSpan), null, CultureInfo.InvariantCulture));
    }
}
