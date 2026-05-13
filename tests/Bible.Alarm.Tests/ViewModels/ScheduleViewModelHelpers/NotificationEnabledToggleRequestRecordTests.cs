#nullable enable

using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class NotificationEnabledToggleRequestRecordTests
{
    [Fact]
    public void Record_is_equal_when_value_log_context_and_slots_match()
    {
        var a = new NotificationEnabledToggleRequest(
            Value: true,
            GetIsGranted: null!,
            RequestPermission: null!,
            SetOffAndNotify: null!,
            SetOnAndNotify: null!,
            SetIsWaitingForPermissionResponse: null!,
            Logger: null!,
            LogContext: "ctx");

        var b = new NotificationEnabledToggleRequest(
            a.Value,
            a.GetIsGranted,
            a.RequestPermission,
            a.SetOffAndNotify,
            a.SetOnAndNotify,
            a.SetIsWaitingForPermissionResponse,
            a.Logger,
            a.LogContext);

        Assert.Equal(a, b);
        Assert.NotEqual(a with { Value = false }, a);
    }
}
