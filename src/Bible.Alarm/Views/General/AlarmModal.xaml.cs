using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.General;

public partial class AlarmModal : BaseContentPage, IDisposable
{
    private bool _isDisposed;

    public AlarmViewModal ViewModel => BindingContext as AlarmViewModal;

    public AlarmModal(AlarmViewModal viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.DismissCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected on page, so dispose it
            if (ViewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}