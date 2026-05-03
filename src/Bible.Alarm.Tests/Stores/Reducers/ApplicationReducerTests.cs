#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ApplicationReducerTests
{
    private static ScheduleStateItem MinimalSchedule(int id = 0, string name = "Test") =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public void OnInitialize_ReplacesScheduleListAndClearsCurrentSchedule()
    {
        var incoming = new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(1, "Loaded") };
        var prior = new ApplicationState([], currentSchedule: MinimalSchedule(99));

        var next = ApplicationReducer.OnInitialize(prior, new InitializeAction(incoming));

        Assert.Same(incoming, next.Schedules);
        Assert.Null(next.CurrentSchedule);
    }

    [Fact]
    public void OnCreateSchedule_DelegatesToScheduleCrudReducer()
    {
        var prior = new ApplicationState([]);
        var draft = MinimalSchedule(0, "From reducer");

        var next = ApplicationReducer.OnCreateSchedule(prior, new CreateScheduleAction(draft));

        Assert.Single(next.Schedules);
        Assert.Equal(-1, next.CurrentSchedule?.Id);
        Assert.Equal("From reducer", next.CurrentSchedule?.Name);
    }

    [Fact]
    public void OnDeleteSchedule_DelegatesToScheduleCrudReducer()
    {
        var item = MinimalSchedule(5);
        var prior = new ApplicationState([item]);

        var next = ApplicationReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(5));

        Assert.Empty(next.Schedules);
    }
}
