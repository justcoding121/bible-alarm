#nullable enable
namespace Bible.Alarm.Services.UI.Interfaces;

public interface INavigationService : IDisposable
{
    Task NavigateToHomeAsync();
    Task NavigateToScheduleAsync();
    Task NavigateToMusicSelectionAsync();
    Task NavigateToSongBookSelectionAsync();
    Task NavigateToTrackSelectionAsync();
    Task NavigateToBibleSelectionAsync();
    Task NavigateToBookSelectionAsync();
    Task NavigateToChapterSelectionAsync();
    Task OpenNumberOfChaptersModalAsync(object bindingContext);
    Task OpenLanguageModalAsync(object bindingContext);
    Task OpenAlarmModalAsync();
    Task OpenBatteryOptimizationModalAsync(object bindingContext);
    Task PopModalAsync();
    Task PopAsync();

    /// <summary>
    /// Gets the BootstrapPage from the navigation stack.
    /// </summary>
    Views.General.BootstrapPage? GetBootstrapPage(bool shouldRetry = true);

    /// <summary>
    /// Pops all modals and pages from the navigation stack, disposing them if they implement IDisposable.
    /// </summary>
    void PopAllModalsAndPages();
    void ClearCache();
}

