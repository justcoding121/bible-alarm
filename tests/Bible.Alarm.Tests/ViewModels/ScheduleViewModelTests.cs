#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests.ViewModels;

[Collection("MauiUi")]
public sealed class ScheduleViewModelTests(MauiUiFixture fixture)
{
    [Fact]
    public void Ctor_initializes_commands_and_new_schedule_flag()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        Assert.NotNull(sut.CancelCommand);
        Assert.NotNull(sut.SaveCommand);
        Assert.NotNull(sut.DeleteCommand);
        Assert.True(sut.IsNewSchedule);
    }

    [Fact]
    public void Ctor_with_existing_schedule_sets_is_new_schedule_false()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), new ScheduleStateItem { Id = 42, Name = "Work" }));
        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps(appState: appState));

        Assert.False(sut.IsNewSchedule);
    }

    [Fact]
    public void IsBusy_forwards_to_property_manager()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.IsBusy = true;

        Assert.True(sut.IsBusy);
    }

    [Fact]
    public void IsMusicSelectionVisible_is_false_when_schedule_is_null()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        Assert.False(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void IsMusicSelectionVisible_is_false_for_music_category_publication()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var schedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationIsMusic = true,
            BiblePublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
        };
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps(appState: appState));

        Assert.False(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void IsMusicSelectionVisible_is_true_for_bible_schedule()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var schedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationIsMusic = false,
            BiblePublicationCode = "nwt",
            BiblePublicationCategoryName = "Bible",
        };
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps(appState: appState));

        Assert.True(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void OnStateChanged_updates_overlay_and_new_schedule_flags()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null));
        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps(appState: appState));

        appState.Value.IsSchedulePageOverlayVisible = true;
        appState.Value.CurrentSchedule = new ScheduleStateItem { Id = 3, Name = "Evening" };
        appState.NotifyChanged();

        Assert.True(sut.IsSchedulePageOverlayVisible);
        Assert.False(sut.IsNewSchedule);
    }

    [Fact]
    public void HideHomePageOverlay_does_not_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.HideHomePageOverlay();
    }

    [Fact]
    public void HideSchedulePageOverlay_does_not_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.HideSchedulePageOverlay();
    }

    [Fact]
    public void OnPageReappearing_skips_before_first_initialization()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.OnPageReappearing();
    }

    [Fact]
    public void ResetContentLoaded_and_OnContentLoaded_do_not_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.ResetContentLoaded();
        sut.OnContentLoaded();
    }

    [Fact]
    public void SetIsSaving_does_not_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.SetIsSaving(true);
        sut.SetIsSaving(false);
    }

    [Fact]
    public void StopPermissionCheckTasks_does_not_throw_on_non_android()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.StopPermissionCheckTasks();
    }

    [Fact]
    public void Dispose_cleans_up_without_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new ScheduleViewModel(ViewModelTestDoubles.CreateScheduleDeps());

        sut.Dispose();
        sut.Dispose();
    }
}
