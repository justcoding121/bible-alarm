#nullable enable

using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class NotificationEnabledToggleRequestRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_primitive_and_null_delegate_slots_are_equal()
    {
        var a = new NotificationEnabledToggleRequest(
            false,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            "");

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
    }
}
