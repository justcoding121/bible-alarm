#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class NotificationEnabledToggleHandlerTests
{
    private static NotificationEnabledToggleRequest Request(
        bool value,
        Func<bool> getGranted,
        Func<bool> requestPermission,
        Action setOff,
        Action setOn,
        Action<bool> setWaiting) =>
        new(
            Value: value,
            GetIsGranted: getGranted,
            RequestPermission: requestPermission,
            SetOffAndNotify: setOff,
            SetOnAndNotify: setOn,
            SetIsWaitingForPermissionResponse: setWaiting,
            Logger: TestLogging.CreateLogger(),
            LogContext: "test");

    [Fact]
    public void TryHandleToggleOnWhenNotGranted_returns_false_when_turning_off()
    {
        var invoked = false;

        var handled = NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
            Request(false, () => true, () => false, () => invoked = true, () => { }, _ => { }));

        Assert.False(handled);
        Assert.False(invoked);
    }

    [Fact]
    public void TryHandleToggleOnWhenNotGranted_returns_false_when_already_granted()
    {
        var handled = NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
            Request(true, () => true, () => false, () => { }, () => { }, _ => { }));

        Assert.False(handled);
    }

    [Fact]
    public void TryHandleToggleOnWhenNotGranted_requests_and_keeps_off_when_permission_flow_returns_false()
    {
        var offCalls = 0;
        var onCalls = 0;
        var waiting = new List<bool>();

        var handled = NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
            Request(
                true,
                () => false,
                () => false,
                () => offCalls++,
                () => onCalls++,
                waiting.Add));

        Assert.True(handled);
        Assert.Equal(1, offCalls);
        Assert.Equal(0, onCalls);
        Assert.Contains(true, waiting);
    }

    [Fact]
    public void TryHandleToggleOnWhenNotGranted_turns_back_on_when_permission_reports_granted_immediately()
    {
        var offCalls = 0;
        var onCalls = 0;

        var handled = NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
            Request(
                true,
                () => false,
                () => true,
                () => offCalls++,
                () => onCalls++,
                _ => { }));

        Assert.True(handled);
        Assert.Equal(1, offCalls);
        Assert.Equal(1, onCalls);
    }

    [Fact]
    public void TryHandleToggleOnWhenNotGranted_handles_grant_check_exception_as_denied()
    {
        var offCalls = 0;
        var waitingFlags = new List<bool>();

        var handled = NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
            Request(
                true,
                () => throw new InvalidOperationException("boom"),
                () => false,
                () => offCalls++,
                () => { },
                waitingFlags.Add));

        Assert.True(handled);
        Assert.Equal(1, offCalls);
        Assert.Contains(true, waitingFlags);
    }
}
