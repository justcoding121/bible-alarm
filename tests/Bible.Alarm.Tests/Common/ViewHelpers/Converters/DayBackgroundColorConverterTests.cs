#nullable enable

using System.Globalization;
using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Bible.Alarm.Shared.Models.Enums;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Bible.Alarm.Tests;

public sealed class DayBackgroundColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private static Color ExpectedDisabledSlot() =>
        ThemeColors.Day.DisabledBackground.Get(ThemeColors.GetCurrentTheme());

    private static Color ExpectedEnabledWhenScheduleAndDayOn()
    {
        var theme = ThemeColors.GetCurrentTheme();
        if (Application.Current?.Resources.TryGetValue("PrimaryColor", out var res) is true && res is Color c)
        {
            return c;
        }

        return ThemeColors.Day.EnabledBackground.Get(theme);
    }

    [Fact]
    public void Convert_null_value_uses_disabled_slot_background()
    {
        var sut = new DayBackgroundColorConverter();
        var expected = ExpectedDisabledSlot();

        Assert.Equal(expected, sut.Convert(null!, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_unsupported_value_type_uses_disabled_slot_background()
    {
        var sut = new DayBackgroundColorConverter();

        Assert.Equal(ExpectedDisabledSlot(), sut.Convert(404, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_weekDays_enabled_day_match_uses_enabled_palette_or_resource()
    {
        var sut = new DayBackgroundColorConverter();
        var expected = ExpectedEnabledWhenScheduleAndDayOn();

        Assert.Equal(expected, sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_weekDays_enabled_day_off_uses_disabled_slot()
    {
        var sut = new DayBackgroundColorConverter();
        var expected = ExpectedDisabledSlot();

        Assert.Equal(expected, sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Tuesday, Cul));
    }

    [Fact]
    public void Convert_parameter_string_parses_weekday()
    {
        var sut = new DayBackgroundColorConverter();
        var expected = ExpectedDisabledSlot();

        Assert.Equal(expected, sut.Convert(WeekDays.Monday, typeof(Color), "Tuesday", Cul));
    }

    [Fact]
    public void MultiConvert_too_few_values_returns_disabled_slot()
    {
        var sut = new DayBackgroundColorConverter();

        Assert.Equal(ExpectedDisabledSlot(), sut.Convert((object[]?)null!, typeof(Color), WeekDays.Monday, Cul));
        Assert.Equal(ExpectedDisabledSlot(), ConvertMulti(sut, Array.Empty<object>()));
        Assert.Equal(ExpectedDisabledSlot(), ConvertMulti(sut, new object[] { WeekDays.Monday }));
    }

    [Fact]
    public void MultiConvert_wrong_types_returns_disabled_slot()
    {
        var sut = new DayBackgroundColorConverter();

        Assert.Equal(ExpectedDisabledSlot(), ConvertMulti(sut, new object[] { "x", true }));
        Assert.Equal(ExpectedDisabledSlot(), ConvertMulti(sut, new object[] { WeekDays.Monday, "x" }));
    }

    [Fact]
    public void MultiConvert_schedule_enabled_day_match_uses_enabled_palette()
    {
        var sut = new DayBackgroundColorConverter();
        var expected = ExpectedEnabledWhenScheduleAndDayOn();

        Assert.Equal(expected, ConvertMulti(sut, new object[] { WeekDays.Monday, true }, WeekDays.Monday));
    }

    [Fact]
    public void MultiConvert_schedule_enabled_day_off_uses_disabled_slot()
    {
        var sut = new DayBackgroundColorConverter();

        Assert.Equal(
            ExpectedDisabledSlot(),
            ConvertMulti(sut, new object[] { WeekDays.Monday, true }, WeekDays.Tuesday));
    }

    [Fact]
    public void MultiConvert_schedule_disabled_day_on_uses_default_background()
    {
        var sut = new DayBackgroundColorConverter();
        var theme = ThemeColors.GetCurrentTheme();
        var expected = ThemeColors.Day.DefaultBackground.Get(theme);

        Assert.Equal(expected, ConvertMulti(sut, new object[] { WeekDays.Monday, false }, WeekDays.Monday));
    }

    [Fact]
    public void MultiConvert_schedule_disabled_day_off_uses_alarm_disabled_pair_background()
    {
        var sut = new DayBackgroundColorConverter();
        var theme = ThemeColors.GetCurrentTheme();
        var expected = ThemeColors.Day.AlarmDisabledDayDisabledBackground.Get(theme);

        Assert.Equal(expected, ConvertMulti(sut, new object[] { WeekDays.Monday, false }, WeekDays.Tuesday));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new DayBackgroundColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Black, typeof(Color), null!, Cul));

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Black, [typeof(WeekDays), typeof(bool)], null!, Cul));
    }

    private static object ConvertMulti(DayBackgroundColorConverter sut, object[] values, object? parameter = null) =>
        sut.Convert(values, typeof(Color), parameter!, Cul);
}
