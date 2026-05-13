#nullable enable

using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerCallbacksRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new HomeStateChangeHandlerCallbacks(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new HomeStateChangeHandlerCallbacks(
            a.SetIsBusy,
            a.GetIsBusy,
            a.GetSchedules,
            a.SetSchedules,
            a.NotifySchedulesChanged,
            a.UpdateProgressBarVisibility,
            a.FadeOutProgressBarAsync,
            a.IsPlaybackModalVisible);

        Assert.Equal(a, b);
    }
}
