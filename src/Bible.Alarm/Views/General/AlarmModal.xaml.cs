using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.General;

public partial class AlarmModal : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly AlarmViewModal _viewModel;

    public AlarmViewModal ViewModel => BindingContext as AlarmViewModal;

    public AlarmModal(AlarmViewModal viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
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
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}