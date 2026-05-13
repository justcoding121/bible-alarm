#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class DeleteScheduleActionTests
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
    public void ScheduleCrudReducer_OnDeleteSchedule_when_schedules_null_returns_original_state()
    {
        var prior = new ApplicationState { Schedules = null!, CurrentSchedule = MinimalSchedule(3) };

        var next = ScheduleCrudReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(3));

        Assert.Null(next.Schedules);
    }

    [Fact]
    public void ScheduleCrudReducer_OnDeleteSchedule_removes_matching_row_keeps_current_when_not_deleted()
    {
        var a = MinimalSchedule(1, "A");
        var b = MinimalSchedule(2, "B");
        var set = new ObservableHashSet<ScheduleStateItem> { a, b };
        var prior = new ApplicationState(set, currentSchedule: a);

        var next = ScheduleCrudReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(2));

        Assert.Single(next.Schedules);
        Assert.Contains(next.Schedules, s => s.Id == 1);
        Assert.Same(a, next.CurrentSchedule);
    }

    [Fact]
    public void ApplicationReducer_OnDeleteSchedule_delegates_to_schedule_crud_reducer()
    {
        var item = MinimalSchedule(5);
        var prior = new ApplicationState([item]);

        var next = ApplicationReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(5));

        Assert.Empty(next.Schedules);
    }
}
