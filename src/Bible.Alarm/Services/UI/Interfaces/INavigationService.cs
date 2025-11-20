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
    MediaElement GetMediaElement();
}

