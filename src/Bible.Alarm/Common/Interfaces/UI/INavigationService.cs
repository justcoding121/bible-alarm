namespace Bible.Alarm.Common.Interfaces.UI;

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
    Task PopAsync();
    Task OpenNumberOfChaptersModalAsync(object bindingContext);
    Task OpenLanguageModalAsync(object bindingContext);
    Task OpenAlarmModalAsync();
    Task OpenMediaProgressModalAsync();
    Task OpenBatteryOptimizationModalAsync(object bindingContext);
    Task CloseModalAsync();
}

