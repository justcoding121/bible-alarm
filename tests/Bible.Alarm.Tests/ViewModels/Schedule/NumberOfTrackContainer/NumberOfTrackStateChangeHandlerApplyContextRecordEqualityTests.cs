#nullable enable

using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTrackStateChangeHandlerApplyContextRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_slots_are_equal()
    {
        var a = new NumberOfTrackStateChangeHandler.ApplyContext(
            null!,
            false,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new NumberOfTrackStateChangeHandler.ApplyContext(
            a.CurrentSchedule,
            a.IsWaitingForPermissionResponse,
            a.GetIsGranted,
            a.DispatchNotificationEnabledOff,
            a.PopulateListViewAsync,
            a.DispatchNumberOfTracksToPlay,
            a.Logger);

        Assert.Equal(a, b);
    }
}
