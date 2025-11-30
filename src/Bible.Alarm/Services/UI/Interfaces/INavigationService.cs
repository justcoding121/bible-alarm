using CommunityToolkit.Maui.Views;

namespace Bible.Alarm.Services.UI.Interfaces;

public interface INavigationService
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
    /// <returns>The BootstrapPage instance, or null if not found</returns>
    Views.General.BootstrapPage? GetBootstrapPage();
}

