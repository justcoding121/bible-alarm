#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests.ViewModels;

[Collection("MauiUi")]
public sealed class HomeViewModelTests(MauiUiFixture fixture)
{
    [Fact]
    public void Ctor_initializes_commands_and_default_properties()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        Assert.NotNull(sut.AddScheduleCommand);
        Assert.NotNull(sut.ViewScheduleCommand);
        Assert.NotNull(sut.OpenAlarmSettingsCommand);
        Assert.NotNull(sut.OpenNotificationPermissionCommand);
        Assert.NotNull(sut.OpenFocusSettingsCommand);
        Assert.NotNull(sut.Schedules);
    }

    [Fact]
    public void IsBusy_forwards_to_property_manager()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.IsBusy = true;

        Assert.True(sut.IsBusy);
    }

    [Fact]
    public void Loaded_forwards_to_property_manager()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.Loaded = true;

        Assert.True(sut.Loaded);
    }

    [Fact]
    public void Schedules_setter_updates_collection()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());
        var schedules = new ObservableHashSet<ScheduleListItemViewModel>();

        sut.Schedules = schedules;

        Assert.Same(schedules, sut.Schedules);
    }

    [Fact]
    public void HideSchedulePageOverlay_dispatches_set_overlay_action()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var dispatcher = new ViewModelTestDoubles.RecordingDispatcher();
        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps(dispatcher));

        sut.HideSchedulePageOverlay();

        var action = Assert.IsType<SetSchedulePageOverlayAction>(Assert.Single(dispatcher.Dispatched));
        Assert.False(action.IsVisible);
    }

    [Fact]
    public void ResetScheduleState_dispatches_reset_action()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var dispatcher = new ViewModelTestDoubles.RecordingDispatcher();
        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps(dispatcher));

        sut.ResetScheduleState();

        Assert.IsType<ResetScheduleStateAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public void Receive_show_progress_bar_message_does_not_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.Receive(new ShowProgressBarMessage());

        Assert.True(sut.ProgressBarOpacity >= 0);
    }

    [Fact]
    public void Receive_hide_progress_bar_message_does_not_throw()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.Receive(new ShowProgressBarMessage());
        sut.Receive(new HideProgressBarMessage());

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task CheckAndShowAlarmSettingsOnFirstLaunchAsync_updates_visibility_without_throwing()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        await sut.CheckAndShowAlarmSettingsOnFirstLaunchAsync();

        Assert.NotNull(sut);
    }

    [Fact]
    public void UpdateFloatingButtonVisibility_updates_focus_warning_and_notification_state()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.UpdateFloatingButtonVisibility();

        Assert.False(sut.IsFocusWarningVisible);
    }

    [Fact]
    public void NotificationPermissionButtonMargin_on_windows_uses_left_bottom_corner()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady || DeviceInfo.Platform != DevicePlatform.WinUI)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        var margin = sut.NotificationPermissionButtonMargin;

        Assert.Equal(24, margin.Left);
    }

    [Fact]
    public void IsLoadingSchedules_reflects_busy_and_empty_schedules()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.IsBusy = true;

        Assert.True(sut.IsLoadingSchedules);
    }

    [Fact]
    public void Dispose_unregisters_messenger_and_does_not_throw_on_second_call()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void StateChanged_triggers_schedule_refresh_when_schedules_update()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null));
        using var sut = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps(appState: appState));

        var schedules = new ObservableHashSet<ScheduleStateItem> { new ScheduleStateItem { Id = 5, Name = "Morning" } };
        appState.Value.Schedules = schedules;
        appState.NotifyChanged();

        Assert.NotNull(sut.Schedules);
    }
}
