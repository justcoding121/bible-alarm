#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class DeleteScheduleFailureActionTests
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
    public void ScheduleCrudReducer_OnDeleteScheduleFailure_reinserts_schedule_when_rollback_payload_is_missing_from_collection()
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
    public void ScheduleCrudReducer_OnDeleteScheduleFailure_returns_original_state_when_schedule_id_already_exists()
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
    public void ScheduleCrudReducer_OnDeleteScheduleFailure_returns_original_state_when_rollback_payload_is_null()
    {
        var prior = new ApplicationState([MinimalSchedule(1)]);
        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(prior, new DeleteScheduleFailureAction(9, "failed", null));

        Assert.Same(prior, next);
    }

    [Fact]
    public void ScheduleCrudReducer_OnDeleteScheduleFailure_returns_original_state_when_schedules_collection_is_null()
    {
        var prior = new ApplicationState { Schedules = null!, CurrentSchedule = MinimalSchedule(3) };
        var next = ScheduleCrudReducer.OnDeleteScheduleFailure(
            prior,
            new DeleteScheduleFailureAction(3, "failed", MinimalSchedule(3)));

        Assert.Same(prior, next);
    }
}
