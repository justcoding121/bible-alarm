#nullable enable
using Bible.Alarm.Views.General;

namespace Bible.Alarm.Services.UI.Interfaces;

public interface INavigationService : IDisposable
{
    Task NavigateToHomeAsync();
    Task NavigateToScheduleAsync();
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
    /// Gets the BootstrapPage from the navigation stack.
    /// </summary>
    BootstrapPage? GetBootstrapPage(bool shouldRetry = true);

    /// <summary>
    /// Pops all modals and pages from the navigation stack, disposing them if they implement IDisposable.
    /// </summary>
    void PopAllModalsAndPages();
    void ClearCache();
}

