namespace Bible.Alarm.Common.Interfaces.UI;

public interface INavigationService : IDisposable
{
    Task Navigate(object viewModel);
    Task GoBack();
    Task ShowModal(string name, object viewModel);
    Task CloseModal();
    event Action<object> NavigatedBack;
    Task NavigateToHome();
    void SetNavigation(Microsoft.Maui.Controls.INavigation navigation);
}