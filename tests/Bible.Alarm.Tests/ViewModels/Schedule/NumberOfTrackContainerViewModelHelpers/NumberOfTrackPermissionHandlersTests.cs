#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainerViewModelHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTrackPermissionHandlersTests
{
    private sealed class UnusedNavigationService : INavigationService
    {
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
        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task PopModalAsync() => Task.CompletedTask;
        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;
        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Views.Home? GetCurrentHomePage() => null;
        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    [Fact]
    public void HandlePermissionGranted_WhenNotWaiting_DoesNotRunCallbacks()
    {
        var setOnCalled = false;
        var updating = new List<bool>();

        NumberOfTrackPermissionHandlers.HandlePermissionGranted(
            isWaitingForPermissionResponse: false,
            TestLogging.CreateLogger(),
            () => setOnCalled = true,
            updating.Add,
            _ => { });

        Assert.False(setOnCalled);
        Assert.Empty(updating);
    }

    [Fact]
    public void HandlePermissionGranted_WhenWaiting_RunsSetOnAndTogglesUpdatingFlags()
    {
        var setOnCalled = false;
        var updating = new List<bool>();
        var waiting = new List<bool>();

        NumberOfTrackPermissionHandlers.HandlePermissionGranted(
            isWaitingForPermissionResponse: true,
            TestLogging.CreateLogger(),
            () => setOnCalled = true,
            updating.Add,
            waiting.Add);

        Assert.True(setOnCalled);
        Assert.Equal(new[] { true, false }, updating);
        Assert.Equal(new[] { false }, waiting);
    }

    [Fact]
    public void HandlePermissionDenied_WhenNotWaiting_DoesNotRunCallbacks()
    {
        var setOffCalled = false;
        var updating = new List<bool>();
        var waiting = new List<bool>();

        NumberOfTrackPermissionHandlers.HandlePermissionDenied(
            isWaitingForPermissionResponse: false,
            TestLogging.CreateLogger(),
            new UnusedNavigationService(),
            () => setOffCalled = true,
            () => { },
            updating.Add,
            waiting.Add);

        Assert.False(setOffCalled);
        Assert.Empty(updating);
        Assert.Empty(waiting);
    }
}
