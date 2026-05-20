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

    [Fact]
    public void OnUpdateScheduleSuccess_updates_existing_schedule_and_current_when_ids_match()
    {
        var existing = MinimalSchedule(8, "Before");
        existing.BiblePublicationLanguageName = "English";
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { existing }, currentSchedule: existing);
        var saved = MinimalSchedule(8, "After");

        var next = ScheduleCrudReducer.OnUpdateScheduleSuccess(prior, new UpdateScheduleSuccessAction(saved));

        Assert.Equal("After", next.Schedules!.Single(s => s.Id == 8).Name);
        Assert.Equal("After", next.CurrentSchedule?.Name);
        Assert.Equal("English", next.Schedules!.Single().BiblePublicationLanguageName);
    }

    [Fact]
    public void OnUpdateScheduleSuccess_adds_schedule_when_missing_from_collection()
    {
        var prior = new ApplicationState(new ObservableHashSet<ScheduleStateItem> { MinimalSchedule(1) });
        var saved = MinimalSchedule(99, "New row");

        var next = ScheduleCrudReducer.OnUpdateScheduleSuccess(prior, new UpdateScheduleSuccessAction(saved));

        Assert.Contains(next.Schedules!, s => s.Id == 99 && s.Name == "New row");
    }

    [Fact]
    public void OnDeleteSchedule_when_schedules_null_returns_unchanged()
    {
        var prior = new ApplicationState([]);
        prior.Schedules = null!;

        var next = ScheduleCrudReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(1));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnDeleteSchedule_removes_row_but_state_factory_preserves_current_reference()
    {
        var target = MinimalSchedule(30);
        var other = MinimalSchedule(31);
        var prior = new ApplicationState([target, other], currentSchedule: target);

        var next = ScheduleCrudReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(30));

        Assert.DoesNotContain(next.Schedules!, s => s.Id == 30);
        Assert.Same(target, next.CurrentSchedule);
    }

    [Fact]
    public void OnCreateScheduleFailure_removes_optimistic_temporary_row()
    {
        var optimistic = MinimalSchedule(-1, "Failed draft");
        var kept = MinimalSchedule(2, "Kept");
        var prior = new ApplicationState([kept, optimistic], currentSchedule: optimistic);

        var next = ScheduleCrudReducer.OnCreateScheduleFailure(
            prior,
            new CreateScheduleFailureAction(optimistic, "db error"));

        Assert.DoesNotContain(next.Schedules!, s => s.Id == -1);
        Assert.Contains(next.Schedules!, s => s.Id == 2);
        Assert.Same(optimistic, next.CurrentSchedule);
    }

    [Fact]
    public void OnDeleteScheduleFailure_returns_unchanged_when_no_schedule_data_for_rollback()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);

        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(99, "network"));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnDeleteScheduleFailure_restores_removed_schedule()
    {
        var remaining = MinimalSchedule(40);
        var restored = MinimalSchedule(41, "Restored");
        var prior = new ApplicationState([remaining]);

        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(41, "network", restored));

        Assert.Equal(2, next.Schedules!.Count);
        Assert.Contains(next.Schedules!, s => s.Id == 41 && s.Name == "Restored");
    }

    [Fact]
    public void OnCreateScheduleSuccess_replaces_optimistic_row_with_confirmed_schedule()
    {
        var optimistic = MinimalSchedule(-1, "Temp");
        var other = MinimalSchedule(3);
        var prior = new ApplicationState([other, optimistic], currentSchedule: optimistic);
        var confirmed = MinimalSchedule(100, "Saved");

        var next = ScheduleCrudReducer.OnCreateScheduleSuccess(
            prior,
            new CreateScheduleSuccessAction(confirmed));

        Assert.DoesNotContain(next.Schedules!, s => s.Id == -1);
        Assert.Contains(next.Schedules!, s => s.Id == 100 && s.Name == "Saved");
        Assert.Equal(-1, next.CurrentSchedule?.Id);
    }

    [Fact]
    public void OnCreateScheduleFailure_returns_unchanged_when_action_schedule_null()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);

        var next = ScheduleCrudReducer.OnCreateScheduleFailure(
            prior,
            new CreateScheduleFailureAction(null!, "error"));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnDeleteScheduleFailure_skips_restore_when_schedule_already_in_collection()
    {
        var existing = MinimalSchedule(41, "Still here");
        var prior = new ApplicationState([existing]);
        var duplicate = MinimalSchedule(41, "Attempt restore");

        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(41, "network", duplicate));

        Assert.Single(next.Schedules!);
        Assert.Same(existing, next.Schedules!.Single());
    }

    [Fact]
    public void OnCreateSchedule_adds_optimistic_row_when_prior_schedules_null()
    {
        var prior = new ApplicationState([]);
        prior.Schedules = null!;
        var draft = MinimalSchedule(0, "New");

        var next = ScheduleCrudReducer.OnCreateSchedule(prior, new CreateScheduleAction(draft));

        Assert.Single(next.Schedules!);
        Assert.Equal(-1, next.Schedules!.Single().Id);
    }

    [Fact]
    public void OnAddScheduleSuccess_appends_schedule_and_sets_current()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);
        var added = MinimalSchedule(77, "Added");

        var next = ScheduleCrudReducer.OnAddScheduleSuccess(prior, new AddScheduleSuccessAction(added));

        Assert.Contains(next.Schedules!, s => s.Id == 77);
        Assert.Equal(77, next.CurrentSchedule?.Id);
    }

    [Fact]
    public void OnCreateSchedule_copies_existing_schedules_before_adding_optimistic_row()
    {
        var existing = MinimalSchedule(5, "Existing");
        var prior = new ApplicationState([existing]);
        var draft = MinimalSchedule(0, "Draft");

        var next = ScheduleCrudReducer.OnCreateSchedule(prior, new CreateScheduleAction(draft));

        Assert.Equal(2, next.Schedules!.Count);
        Assert.Contains(next.Schedules!, s => s.Id == 5 && s.Name == "Existing");
        Assert.Contains(next.Schedules!, s => s.Id == -1 && s.Name == "Draft");
    }

    [Fact]
    public void OnCreateScheduleSuccess_returns_unchanged_when_schedule_null()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);

        var next = ScheduleCrudReducer.OnCreateScheduleSuccess(
            prior,
            new CreateScheduleSuccessAction(null!));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnCreateScheduleSuccess_returns_unchanged_when_state_schedules_null()
    {
        var prior = new ApplicationState([]);
        prior.Schedules = null!;
        var confirmed = MinimalSchedule(10);

        var next = ScheduleCrudReducer.OnCreateScheduleSuccess(
            prior,
            new CreateScheduleSuccessAction(confirmed));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnUpdateScheduleSuccess_returns_unchanged_when_state_schedules_null()
    {
        var prior = new ApplicationState([]);
        prior.Schedules = null!;
        var saved = MinimalSchedule(8);

        var next = ScheduleCrudReducer.OnUpdateScheduleSuccess(
            prior,
            new UpdateScheduleSuccessAction(saved));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnRemoveScheduleSuccess_returns_unchanged_when_state_schedules_null()
    {
        var prior = new ApplicationState([]);
        prior.Schedules = null!;

        var next = ScheduleCrudReducer.OnRemoveScheduleSuccess(
            prior,
            new RemoveScheduleSuccessAction(1));

        Assert.Same(prior, next);
    }
}
