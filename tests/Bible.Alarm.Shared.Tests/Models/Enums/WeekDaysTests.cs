#nullable enable

using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class WeekDaysTests
{
    [Fact]
    public void ToList_maps_selected_weekdays_to_one_based_positions_skipping_All()
    {
        var days = WeekDays.Monday | WeekDays.Wednesday;

        Assert.Equal(new List<int> { 2, 4 }, days.ToList());
    }

    [Fact]
    public void ToList_includes_only_Sunday_when_that_flag_set()
    {
        var days = WeekDays.Sunday;

        Assert.Equal(new List<int> { 1 }, days.ToList());
    }
}
