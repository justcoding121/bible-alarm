#nullable enable

using System.Globalization;
using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Bible.Alarm.Shared.Models.Enums;
using Microsoft.Maui.Graphics;

namespace Bible.Alarm.Tests;

public sealed class DayColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private static Color EnabledText() =>
        ThemeColors.Day.EnabledText.Get(ThemeColors.GetCurrentTheme());

    private static Color MutedText() =>
        ThemeColors.Day.CalendarMutedText.Get(ThemeColors.GetCurrentTheme());

    [Fact]
    public void Convert_null_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, sut.Convert(null!, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_weekDays_day_match_uses_enabled_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(EnabledText(), sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_weekDays_day_off_uses_muted_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Tuesday, Cul));
    }

    [Fact]
    public void Convert_parameter_string_parses_weekday()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), sut.Convert(WeekDays.Monday, typeof(Color), "Tuesday", Cul));
    }

    [Fact]
    public void Convert_unrecognized_type_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, sut.Convert(12, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void MultiConvert_too_few_or_null_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, sut.Convert((object[]?)null!, typeof(Color), WeekDays.Monday, Cul));
        Assert.Equal(Colors.White, Multi(sut, Array.Empty<object>()));
        Assert.Equal(Colors.White, Multi(sut, new object[] { WeekDays.Monday }));
    }

    [Fact]
    public void MultiConvert_wrong_element_types_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, Multi(sut, new object[] { "x", true }));
        Assert.Equal(Colors.White, Multi(sut, new object[] { WeekDays.Monday, "x" }));
    }

    [Fact]
    public void MultiConvert_schedule_on_day_match_uses_enabled_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(EnabledText(), Multi(sut, new object[] { WeekDays.Monday, true }, WeekDays.Monday));
    }

    [Fact]
    public void MultiConvert_schedule_on_day_off_uses_muted_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), Multi(sut, new object[] { WeekDays.Monday, true }, WeekDays.Tuesday));
    }

    [Fact]
    public void MultiConvert_schedule_off_always_muted_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), Multi(sut, new object[] { WeekDays.Monday, false }, WeekDays.Monday));
        Assert.Equal(MutedText(), Multi(sut, new object[] { WeekDays.Monday, false }, WeekDays.Tuesday));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new DayColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.White, typeof(Color), null!, Cul));

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.White, [typeof(WeekDays), typeof(bool)], null!, Cul));
    }

    private static object Multi(DayColorConverter sut, object[] values, object? parameter = null) =>
        sut.Convert(values, typeof(Color), parameter!, Cul);
}
