#nullable enable

using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class WeekDaysExtensionsTests
{
    [Fact]
    public void ToList_ReturnsEmpty_WhenNoBitsSet()
    {
        Assert.Empty(((WeekDays)0).ToList());
    }

    [Fact]
    public void ToList_SingleDay_ReturnsOneBasedIndexWithSundayAsOne()
    {
        Assert.Equal(new[] { 1 }, WeekDays.Sunday.ToList());
        Assert.Equal(new[] { 2 }, WeekDays.Monday.ToList());
        Assert.Equal(new[] { 7 }, WeekDays.Saturday.ToList());
    }

    [Fact]
    public void ToList_CombinesSeveralDays_InCalendarOrder()
    {
        var monFri = WeekDays.Monday | WeekDays.Friday;
        Assert.Equal(new[] { 2, 6 }, monFri.ToList());

        var sunSat = WeekDays.Sunday | WeekDays.Saturday;
        Assert.Equal(new[] { 1, 7 }, sunSat.ToList());
    }

    [Fact]
    public void ToList_All_ExpandsEveryWeekdaySkippingTheAllMask()
    {
        var list = WeekDays.All.ToList();
        Assert.Equal(7, list.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, list);
    }

    [Fact]
    public void ToList_WeekdaysBitset_MondayThroughFriday()
    {
        var weekdays = WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday |
                       WeekDays.Thursday | WeekDays.Friday;
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, weekdays.ToList());
    }
}
