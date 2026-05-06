#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTrackStateChangeHandlerTests
{
    [Fact]
    public void Syncs_notification_toggle_and_schedule_fields()
    {
        var schedule = new ScheduleStateItem
        {
            NotificationEnabled = true,
            AlwaysPlayFromStart = true,
            NumberOfTracksToPlay = 2,
            BiblePublicationCategoryName = "Bible",
        };
        var targets = new NumberOfTrackStateChangeHandler.SyncTargets
        {
            NotificationEnabled = false,
            LastCategoryName = "Bible",
        };
        var populate = new List<int>();

        var ctx = new NumberOfTrackStateChangeHandler.ApplyContext(
            schedule,
            IsWaitingForPermissionResponse: false,
            () => true,
            () => { },
            async n =>
            {
                populate.Add(n);
                await Task.CompletedTask;
            },
            () => throw new InvalidOperationException("should not dispatch play count"),
            TestLogging.CreateLogger());

        NumberOfTrackStateChangeHandler.ApplyPropertyChanges(ctx, targets);

        Assert.True(targets.NotificationEnabled);
        Assert.True(targets.AlwaysPlayFromStart);
        Assert.False(targets.PlayIndefinitely);
        Assert.Empty(populate);
    }

    [Fact]
    public void Category_change_repairs_list_and_dispatches_when_track_cap_positive()
    {
        var schedule = new ScheduleStateItem
        {
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
            BiblePublicationCategoryName = "Dramas",
        };
        var targets = new NumberOfTrackStateChangeHandler.SyncTargets
        {
            NotificationEnabled = true,
            LastCategoryName = "Bible",
        };
        var populate = new List<int>();
        var dispatchPlay = 0;

        var ctx = new NumberOfTrackStateChangeHandler.ApplyContext(
            schedule,
            IsWaitingForPermissionResponse: false,
            () => true,
            () => { },
            async n =>
            {
                populate.Add(n);
                await Task.CompletedTask;
            },
            () => dispatchPlay++,
            TestLogging.CreateLogger());

        NumberOfTrackStateChangeHandler.ApplyPropertyChanges(ctx, targets);

        Assert.Equal([1], populate);
        Assert.Equal(1, dispatchPlay);
        Assert.Equal("Dramas", targets.LastCategoryName);
    }

    [Fact]
    public void Category_change_skips_dispatch_when_playing_indefinitely()
    {
        var schedule = new ScheduleStateItem
        {
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 0,
            BiblePublicationCategoryName = "Music",
        };
        var targets = new NumberOfTrackStateChangeHandler.SyncTargets
        {
            NotificationEnabled = true,
            LastCategoryName = "Bible",
        };
        var populate = new List<int>();
        var dispatchPlay = 0;

        var ctx = new NumberOfTrackStateChangeHandler.ApplyContext(
            schedule,
            IsWaitingForPermissionResponse: false,
            () => true,
            () => { },
            async n =>
            {
                populate.Add(n);
                await Task.CompletedTask;
            },
            () => dispatchPlay++,
            TestLogging.CreateLogger());

        NumberOfTrackStateChangeHandler.ApplyPropertyChanges(ctx, targets);

        Assert.Single(populate);
        Assert.Equal(0, dispatchPlay);
        Assert.True(targets.PlayIndefinitely);
    }

    [Fact]
    public void Skips_notification_resync_while_waiting_for_permission_response()
    {
        var schedule = new ScheduleStateItem
        {
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 1,
            BiblePublicationCategoryName = "Bible",
        };
        var targets = new NumberOfTrackStateChangeHandler.SyncTargets
        {
            NotificationEnabled = false,
            LastCategoryName = "Bible",
        };
        var ctx = new NumberOfTrackStateChangeHandler.ApplyContext(
            schedule,
            IsWaitingForPermissionResponse: true,
            () => true,
            () => throw new InvalidOperationException("should not dispatch off while waiting"),
            async _ => await Task.CompletedTask,
            () => throw new InvalidOperationException("should not dispatch play count"),
            TestLogging.CreateLogger());

        NumberOfTrackStateChangeHandler.ApplyPropertyChanges(ctx, targets);

        Assert.False(targets.NotificationEnabled);
        Assert.False(targets.AlwaysPlayFromStart);
        Assert.False(targets.PlayIndefinitely);
    }

    [Fact]
    public void Category_change_ignored_when_name_differs_only_by_case()
    {
        var schedule = new ScheduleStateItem
        {
            NotificationEnabled = true,
            AlwaysPlayFromStart = false,
            NumberOfTracksToPlay = 3,
            BiblePublicationCategoryName = "DRAMAS",
        };
        var targets = new NumberOfTrackStateChangeHandler.SyncTargets
        {
            NotificationEnabled = true,
            LastCategoryName = "dramas",
        };
        var populate = new List<int>();
        var dispatchPlay = 0;

        var ctx = new NumberOfTrackStateChangeHandler.ApplyContext(
            schedule,
            IsWaitingForPermissionResponse: false,
            () => true,
            () => { },
            async n =>
            {
                populate.Add(n);
                await Task.CompletedTask;
            },
            () => dispatchPlay++,
            TestLogging.CreateLogger());

        NumberOfTrackStateChangeHandler.ApplyPropertyChanges(ctx, targets);

        Assert.Empty(populate);
        Assert.Equal(0, dispatchPlay);
        Assert.Equal("DRAMAS", targets.LastCategoryName);
    }
}
