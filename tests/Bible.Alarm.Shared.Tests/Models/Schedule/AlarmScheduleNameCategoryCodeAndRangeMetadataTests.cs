#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleNameCategoryCodeAndRangeMetadataTests
{
    [Fact]
    public void Name_and_category_code_max_lengths_match_contract()
    {
        Assert.Equal(255, MaxLengthOf(nameof(AlarmSchedule.Name)));
        Assert.Equal(100, MaxLengthOf(nameof(AlarmSchedule.CategoryCode)));
    }

    [Fact]
    public void Time_snooze_and_track_count_ranges_match_contract()
    {
        AssertRange(nameof(AlarmSchedule.Hour), 0, 23);
        AssertRange(nameof(AlarmSchedule.Minute), 0, 59);
        AssertRange(nameof(AlarmSchedule.Second), 0, 59);
        AssertRange(nameof(AlarmSchedule.SnoozeMinutes), 1, 60);
        AssertRange(nameof(AlarmSchedule.NumberOfTracksToPlay), 0, 21);
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(AlarmSchedule).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;

    private static void AssertRange(string propertyName, int min, int max)
    {
        var r = typeof(AlarmSchedule).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(RangeAttribute), inherit: false)
            .Cast<RangeAttribute>()
            .Single();
        Assert.Equal(min, r.Minimum);
        Assert.Equal(max, r.Maximum);
    }
}
