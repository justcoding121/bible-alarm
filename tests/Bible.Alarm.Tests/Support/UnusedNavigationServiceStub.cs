#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Views;

namespace Bible.Alarm.Tests.Support;

/// <summary>
/// Navigation stub for headless MAUI tests that never navigate.
/// </summary>
internal sealed class UnusedNavigationServiceStub : INavigationService
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

    public Home? GetCurrentHomePage() => null;

    public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;

    public bool IsPlaybackModalOnScreen() => false;

    public void SetMiniBarVisible(bool visible)
    {
    }
}
