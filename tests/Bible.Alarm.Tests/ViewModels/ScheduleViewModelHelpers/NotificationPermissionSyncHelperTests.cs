#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class NotificationPermissionSyncHelperTests
{
    private readonly string _syncMsg = "sync off";
    private readonly string _ctx = "ctx";

    [Fact]
    public void False_value_returns_without_dispatch_or_permission_check()
    {
        var logger = TestLogging.CreateLogger();
        var dispatchCalls = 0;

        var result = NotificationPermissionSyncHelper.SyncValueWithPermission(
            false,
            () => throw new InvalidOperationException("should not run"),
            logger,
            _syncMsg,
            _ctx,
            () => dispatchCalls++);

        Assert.False(result);
        Assert.Equal(0, dispatchCalls);
    }

    [Fact]
    public void True_when_permission_already_granted()
    {
        var logger = TestLogging.CreateLogger();
        var dispatchCalls = 0;

        var result = NotificationPermissionSyncHelper.SyncValueWithPermission(
            true,
            () => true,
            logger,
            _syncMsg,
            _ctx,
            () => dispatchCalls++);

        Assert.True(result);
        Assert.Equal(0, dispatchCalls);
    }

    [Fact]
    public void True_but_not_granted_dispatches_off_and_returns_false()
    {
        var logger = TestLogging.CreateLogger();
        var dispatchCalls = 0;

        var result = NotificationPermissionSyncHelper.SyncValueWithPermission(
            true,
            () => false,
            logger,
            _syncMsg,
            _ctx,
            () => dispatchCalls++);

        Assert.False(result);
        Assert.Equal(1, dispatchCalls);
    }

    [Fact]
    public void Permission_check_exception_dispatches_off_and_returns_false()
    {
        var logger = TestLogging.CreateLogger();
        var dispatchCalls = 0;

        var result = NotificationPermissionSyncHelper.SyncValueWithPermission(
            true,
            () => throw new InvalidOperationException("platform"),
            logger,
            _syncMsg,
            _ctx,
            () => dispatchCalls++);

        Assert.False(result);
        Assert.Equal(1, dispatchCalls);
    }
}
