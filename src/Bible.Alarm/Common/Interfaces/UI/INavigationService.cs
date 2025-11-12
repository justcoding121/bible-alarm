namespace Bible.Alarm.Common.Interfaces.UI;

public interface INavigationService
{
    Task NavigateToMusicSelectionAsync();
    Task NavigateToBibleSelectionAsync();
    Task OpenNumberOfChaptersModalAsync(object bindingContext);
    Task CloseModalAsync();
}

