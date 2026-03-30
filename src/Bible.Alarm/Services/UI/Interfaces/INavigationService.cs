#nullable enable

using Bible.Alarm.Views;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Services.UI.Interfaces;

public interface INavigationService : IDisposable
{
    Task NavigateToHomeAsync(bool animated = true);
    Task NavigateToScheduleAsync();

    /// <summary>
    /// Navigates to an existing schedule by ID. The schedule will be loaded from DB inside the navigation lock.
    /// </summary>
    Task NavigateToScheduleAsync(int scheduleId, bool isEnabled);
    Task OpenSongPublicationSelectionModalAsync(object bindingContext);
    Task OpenMusicTrackSelectionModalAsync(object bindingContext);
    Task OpenBibleSelectionModalAsync(object bindingContext);
    Task OpenSectionSelectionModalAsync(object bindingContext);
    Task OpenMusicSectionSelectionModalAsync(object bindingContext);
    Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext);
    Task OpenNumberOfTracksModalAsync(object bindingContext);
    Task OpenLanguageModalAsync(object bindingContext);
    Task OpenCategoryModalAsync(object bindingContext);
    Task OpenPlaybackModalAsync();
    /// <summary>
    /// Opens PlaybackModal with control over whether Home should be revealed (opacity=1) behind it once rendered.
    /// </summary>
    Task OpenPlaybackModalAsync(bool revealHomeBehindModalOnLoad, bool animated = false);
    Task OpenBatteryOptimizationModalAsync(object bindingContext);
    Task OpenNotificationPermissionModalAsync(object bindingContext);
    Task PopModalAsync();
    Task PopAsync();

    /// <summary>
    /// Pops only the PlaybackModal page from the navigation stack,
    /// leaving all other pages/modals intact.
    /// </summary>
    Task PopPlaybackPageAsync(bool animated = false);

    /// <summary>
    /// Pops all modals and pages from the navigation stack, disposing them if they implement IDisposable.
    /// </summary>
    void PopAllModalsAndPages();
    void ClearCache();

    /// <summary>
    /// Gets the current Home page from the navigation stack, if available.
    /// </summary>
    Home? GetCurrentHomePage();

    /// <summary>
    /// Gets the top page of the navigation stack (visible when no modal is shown).
    /// </summary>
    Page? GetCurrentPage();

    /// <summary>
    /// Sets Home page visibility based on playback state.
    /// If playback is active, hides Home to prevent visual flash before playback modal appears.
    /// </summary>
    void SetHomePageVisibility(bool isPlaybackActive);

    /// <summary>
    /// Returns true if a PlaybackModal is currently in the navigation stack.
    /// </summary>
    bool IsPlaybackModalOnScreen();

    /// <summary>
    /// Shows or hides the mini playback bar via the shared ViewModel.
    /// </summary>
    void SetMiniBarVisible(bool visible);
}

