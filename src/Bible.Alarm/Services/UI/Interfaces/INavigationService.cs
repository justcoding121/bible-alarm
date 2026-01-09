#nullable enable

using Bible.Alarm.Views;

namespace Bible.Alarm.Services.UI.Interfaces;

public interface INavigationService : IDisposable
{
    Task NavigateToHomeAsync();
    Task NavigateToScheduleAsync();
    
    /// <summary>
    /// Navigates to an existing schedule by ID. The schedule will be loaded from DB inside the navigation lock.
    /// </summary>
    Task NavigateToScheduleAsync(int scheduleId, bool isEnabled);
    Task OpenMusicSelectionModalAsync(object bindingContext);
    Task OpenSongBookSelectionModalAsync(object bindingContext);
    Task OpenTrackSelectionModalAsync(object bindingContext);
    Task OpenBibleSelectionModalAsync(object bindingContext);
    Task OpenBookSelectionModalAsync(object bindingContext);
    Task OpenChapterSelectionModalAsync(object bindingContext);
    Task OpenNumberOfChaptersModalAsync(object bindingContext);
    Task OpenLanguageModalAsync(object bindingContext);
    Task OpenAlarmModalAsync();
    Task OpenBatteryOptimizationModalAsync(object bindingContext);
    Task PopModalAsync();
    Task PopAsync();

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
    /// Sets Home page visibility based on playback state.
    /// If playback is active, hides Home to prevent visual flash before alarm modal appears.
    /// </summary>
    void SetHomePageVisibility(bool isPlaybackActive);
}

