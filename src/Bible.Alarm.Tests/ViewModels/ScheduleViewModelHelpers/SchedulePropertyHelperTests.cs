#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class SchedulePropertyHelperTests
{
    [Fact]
    public void Null_schedule_returns_safe_defaults()
    {
        Assert.Equal(string.Empty, SchedulePropertyHelper.GetName(null));
        Assert.False(SchedulePropertyHelper.GetIsEnabled(null));
        Assert.Equal((WeekDays)0, SchedulePropertyHelper.GetDaysOfWeek(null));
        Assert.Equal(TimeSpan.Zero, SchedulePropertyHelper.GetTime(null));
        Assert.False(SchedulePropertyHelper.GetMusicEnabled(null));
        Assert.Equal(0, SchedulePropertyHelper.GetScheduleId(null));
    }

    [Fact]
    public void Populated_schedule_maps_properties()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 42,
            Name = "Morning",
            IsEnabled = true,
            Hour = 7,
            Minute = 30,
            Second = 5,
            DaysOfWeek = WeekDays.Monday | WeekDays.Wednesday,
            MusicEnabled = true,
        };

        Assert.Equal("Morning", SchedulePropertyHelper.GetName(schedule));
        Assert.True(SchedulePropertyHelper.GetIsEnabled(schedule));
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, SchedulePropertyHelper.GetDaysOfWeek(schedule));
        Assert.Equal(new TimeSpan(7, 30, 5), SchedulePropertyHelper.GetTime(schedule));
        Assert.True(SchedulePropertyHelper.GetMusicEnabled(schedule));
        Assert.Equal(42, SchedulePropertyHelper.GetScheduleId(schedule));
    }
}
