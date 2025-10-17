using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Contracts
{
    public interface INavigationService: IDisposable
    {
        Task Navigate(object viewModel);
        Task GoBack();
        Task ShowModal(string name, object viewModel);
        Task CloseModal();
        event Action<object> NavigatedBack;
        Task NavigateToHome();
    }
}
