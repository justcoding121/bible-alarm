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
    public void OnUpdateScheduleFromViewModel_should_save_updates_matching_schedule_in_collection()
    {
        var existing = MinimalSchedule(5, "Old");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { existing }, currentSchedule: existing);
        var updated = MinimalSchedule(5, "Renamed");

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(updated, shouldSave: true));

        Assert.Equal("Renamed", next.Schedules!.Single(s => s.Id == 5).Name);
        Assert.Equal("Renamed", next.CurrentSchedule?.Name);
    }

    [Fact]
    public void OnUpdateScheduleFromViewModel_should_save_false_updates_current_without_changing_list()
    {
        var listItem = MinimalSchedule(5, "Persisted");
        var draft = MinimalSchedule(5, "Draft edit");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { listItem }, currentSchedule: draft);

        var next = ApplicationReducer.OnUpdateScheduleFromViewModel(
            prior,
            new UpdateScheduleFromViewModelAction(draft, shouldSave: false));

        Assert.Equal("Persisted", next.Schedules!.Single().Name);
        Assert.Equal("Draft edit", next.CurrentSchedule?.Name);
    }

    [Fact]
    public void OnUpdateScheduleSuccess_delegates_to_schedule_crud_reducer()
    {
        var existing = MinimalSchedule(8, "Before");
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { existing });
        var saved = MinimalSchedule(8, "After");

        var next = ApplicationReducer.OnUpdateScheduleSuccess(prior, new UpdateScheduleSuccessAction(saved));

        Assert.Equal("After", next.Schedules!.Single(s => s.Id == 8).Name);
    }

    [Fact]
    public void OnUpdateScheduleFailure_returns_original_state_reference()
    {
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(2) });
        var failed = MinimalSchedule(2, "Failed");

        var next = ApplicationReducer.OnUpdateScheduleFailure(
            prior,
            new UpdateScheduleFailureAction(failed, "error"));

        Assert.Same(prior, next);
    }
}
