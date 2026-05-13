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
    public void OnDeleteSchedule_WhenSchedulesNull_ReturnsOriginalState()
    {
        var prior = new ApplicationState { Schedules = null!, CurrentSchedule = MinimalSchedule(3) };

        var next = ScheduleCrudReducer.OnDeleteSchedule(prior, new DeleteScheduleAction(3));

        Assert.Null(next.Schedules);
    }

    [Fact]
    public void OnDeleteSchedule_RemovesMatchingRow_KeepsCurrentWhenNotDeleted()
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
    public void OnDeleteScheduleFailure_ReinsertsScheduleWhenRollbackPayloadIsMissingFromCollection()
    {
        var kept = MinimalSchedule(1, "Kept");
        var prior = new ApplicationState([kept]);
        var rollbackPayload = MinimalSchedule(99, "Restored");

        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(rollbackPayload.Id, "failed", rollbackPayload));

        Assert.Equal(2, next.Schedules.Count);
        var inserted = Assert.Single(next.Schedules, s => s.Id == 99);
        Assert.Equal("Restored", inserted.Name);
        Assert.NotSame(rollbackPayload, inserted);
    }

    [Fact]
    public void OnDeleteScheduleFailure_ReturnsOriginalStateWhenScheduleIdAlreadyExists()
    {
        var existing = MinimalSchedule(7, "Existing");
        var prior = new ApplicationState([existing]);
        var rollbackPayload = MinimalSchedule(7, "RollbackSnapshot");

        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(7, "failed", rollbackPayload));

        Assert.Same(prior, next);
        Assert.Same(existing, Assert.Single(next.Schedules));
    }

    [Fact]
    public void OnDeleteScheduleFailure_ReturnsOriginalStateWhenRollbackPayloadIsNull()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);
        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(prior, new DeleteScheduleFailureAction(9, "failed", null));

        Assert.Same(prior, next);
    }

    [Fact]
    public void OnDeleteScheduleFailure_ReturnsOriginalStateWhenSchedulesCollectionIsNull()
    {
        var prior = new ApplicationState { Schedules = null!, CurrentSchedule = MinimalSchedule(3) };
        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(3, "failed", MinimalSchedule(3)));

        Assert.Same(prior, next);
    }
}
