#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationTests
{
    [Fact]
    public void Properties_assign_and_roundtrip_on_entity()
    {
        var scheduled = DateTimeOffset.Parse("2027-06-01T08:09:10Z");

        var schedule = new AlarmSchedule
        {
            Id = 7,
            Name = "Parent",
            IsEnabled = true,
            Hour = 8,
            Minute = 9,
            Second = 10,
            DaysOfWeek = WeekDays.Monday | WeekDays.Wednesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

        var sut = new AlarmNotification
        {
            Id = 100,
            ScheduledTime = scheduled,
            Sent = false,
            Fired = false,
            AlarmScheduleId = schedule.Id,
            AlarmSchedule = schedule,
            CancellationRequested = true,
            Cancelled = false,
        };

        Assert.Equal(100L, sut.Id);
        Assert.Equal(scheduled, sut.ScheduledTime);
        Assert.False(sut.Sent);
        Assert.False(sut.Fired);
        Assert.Equal(7, sut.AlarmScheduleId);
        Assert.Same(schedule, sut.AlarmSchedule);
        Assert.True(sut.CancellationRequested);
        Assert.False(sut.Cancelled);

        sut.Sent = true;
        sut.Cancelled = true;
        Assert.True(sut.Sent);
        Assert.True(sut.Cancelled);
    }

    [Fact]
    public void Default_ctor_reports_clr_defaults_then_fields_mutate()
    {
        var sut = new AlarmNotification();
        Assert.Equal(0L, sut.Id);
        Assert.Equal(default, sut.ScheduledTime);
        Assert.False(sut.Sent);
        Assert.False(sut.Fired);
        Assert.Equal(0, sut.AlarmScheduleId);
        Assert.Null(sut.AlarmSchedule);
        Assert.False(sut.CancellationRequested);
        Assert.False(sut.Cancelled);

        var anchor = DateTimeOffset.Parse("2030-01-02T03:04:05+00:00");
        sut.ScheduledTime = anchor;
        sut.AlarmScheduleId = 502;
        sut.Fired = true;
        sut.CancellationRequested = true;

        Assert.Equal(anchor, sut.ScheduledTime);
        Assert.Equal(502, sut.AlarmScheduleId);
        Assert.True(sut.Fired);
        Assert.True(sut.CancellationRequested);
    }

    [Fact]
    public void Schedule_navigation_assignable_after_notifications_created()
    {
        var schedule = new AlarmSchedule
        {
            Id = 3,
            Name = "Late bind",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 0,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
            LatestAlarmNotificationId = 0,
        };

        var sut = new AlarmNotification
        {
            ScheduledTime = DateTimeOffset.Parse("2031-11-30T06:45:00Z"),
            AlarmScheduleId = schedule.Id,
        };

        sut.AlarmSchedule = schedule;

        Assert.Same(schedule, sut.AlarmSchedule);
        Assert.Equal(schedule.Id, sut.AlarmScheduleId);
    }
}
