#nullable enable

using System.Reflection;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Views;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class NotificationPermissionDeniedModalHelperTests
{
    private sealed class NavigationStub : INavigationService
    {
        public bool ThrowOnOpen { get; init; }

        public List<object> NotificationModalContexts { get; } = [];

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;

        public Task NavigateToScheduleAsync() => Task.CompletedTask;

        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;

        public Task OpenSongPublicationSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenMusicTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenMusicSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenLanguageModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;

        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;

        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;

        public bool DisposeViewModelOnOpen { get; init; } = true;

        public Task OpenNotificationPermissionModalAsync(object bindingContext)
        {
            NotificationModalContexts.Add(bindingContext);
            if (DisposeViewModelOnOpen && bindingContext is NotificationPermissionViewModel vm)
            {
                vm.Dispose();
            }

            if (ThrowOnOpen)
            {
                throw new InvalidOperationException("nav failed");
            }

            return Task.CompletedTask;
        }

        public Task PopModalAsync() => Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Home? GetCurrentHomePage() => null;

        public Page? GetCurrentPage() => null;

        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    [Fact]
    public async Task ShowAsync_opens_notification_permission_modal_with_view_model()
    {
        var nav = new NavigationStub();

        await NotificationPermissionDeniedModalHelper.ShowAsync(
            TestLogging.CreateLogger(),
            nav,
            onPermissionGranted: () => { });

        var vm = Assert.Single(nav.NotificationModalContexts);
        Assert.IsType<NotificationPermissionViewModel>(vm);
    }

    [Fact]
    public async Task ShowAsync_wires_modal_dismiss_callback_for_granted_permission()
    {
        var nav = new NavigationStub { DisposeViewModelOnOpen = false };

        await NotificationPermissionDeniedModalHelper.ShowAsync(
            TestLogging.CreateLogger(),
            nav,
            onPermissionGranted: () => { });

        var vm = Assert.IsType<NotificationPermissionViewModel>(Assert.Single(nav.NotificationModalContexts));
        var invoke = typeof(NotificationPermissionViewModel).GetMethod(
            "InvokeModalDismissedCallbackIfProvided",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(invoke);

        var ex = Record.Exception(() => invoke!.Invoke(vm, [true]));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ShowAsync_does_not_throw_when_navigation_fails()
    {
        var nav = new NavigationStub { ThrowOnOpen = true };

        await NotificationPermissionDeniedModalHelper.ShowAsync(
            TestLogging.CreateLogger(),
            nav,
            onPermissionGranted: () => { });

        Assert.Single(nav.NotificationModalContexts);
    }
}
