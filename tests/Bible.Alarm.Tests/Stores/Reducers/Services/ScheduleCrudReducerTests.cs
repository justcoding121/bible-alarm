#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCrudReducerTests
{
    private static ScheduleStateItem MinimalSchedule(int id = 0, string name = "Morning") =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public void OnCreateSchedule_WhenScheduleNull_ReturnsOriginalState()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);

        var next = ScheduleCrudReducer.OnCreateSchedule(prior, new CreateScheduleAction(null!));

        Assert.Same(prior.Schedules, next.Schedules);
        Assert.Equal(prior.Schedules.Count, next.Schedules.Count);
    }

    [Fact]
    public void OnCreateSchedule_WhenValid_AddsOptimisticRowWithTemporaryId()
    {
        var prior = new ApplicationState([]);
        var draft = MinimalSchedule(id: 0, name: "New alarm");

        var next = ScheduleCrudReducer.OnCreateSchedule(prior, new CreateScheduleAction(draft));

        Assert.Single(next.Schedules);
        Assert.Contains(next.Schedules, s => s.Id == -1 && s.Name == "New alarm");
        Assert.NotNull(next.CurrentSchedule);
        Assert.Equal(-1, next.CurrentSchedule!.Id);
    }

    [Fact]
    public void OnRemoveScheduleSuccess_RemovesById_KeepsCurrentWhenNotRemoved()
    {
        var a = MinimalSchedule(10);
        var b = MinimalSchedule(20);
        var prior = new ApplicationState([a, b], currentSchedule: b);

        var next = ScheduleCrudReducer.OnRemoveScheduleSuccess(prior, new RemoveScheduleSuccessAction(10));

        Assert.Single(next.Schedules);
        Assert.DoesNotContain(next.Schedules, s => s.Id == 10);
        Assert.Same(b, next.CurrentSchedule);
    }
}
