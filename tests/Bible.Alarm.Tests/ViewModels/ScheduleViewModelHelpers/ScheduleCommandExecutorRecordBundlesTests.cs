#nullable enable

using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCommandExecutorRecordBundlesTests
{
    [Fact]
    public void CoreDeps_instances_with_null_collaborators_compare_equal()
    {
        var a = new ScheduleCommandExecutorCoreDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new ScheduleCommandExecutorCoreDeps(
            a.ScheduleCommandService,
            a.ScheduleMediaCacheService,
            a.State,
            a.PlaybackState,
            a.Dispatcher,
            a.Mapper,
            a.Logger);

        Assert.Equal(a, b);
    }

    [Fact]
    public void UiHooks_instances_with_null_callbacks_compare_equal()
    {
        var a = new ScheduleCommandExecutorUiHooks(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new ScheduleCommandExecutorUiHooks(
            a.GetMusicSelectionContainerViewModel,
            a.GetAlarmSettingsContainerViewModel,
            a.GetNumberOfTrackContainerViewModel,
            a.SetIsSaving,
            a.SetIsCancelBusy,
            a.SetIsSaveBusy,
            a.SetIsDeleteBusy);

        Assert.Equal(a, b);
    }
}
