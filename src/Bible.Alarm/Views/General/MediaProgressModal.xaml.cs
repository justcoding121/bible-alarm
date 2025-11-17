#nullable enable
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MediaProgressModal : ContentPage, IDisposable
{
    private bool _isDisposed;
    private MediaProgressViewModal? _viewModel;

    public MediaProgressModal(MediaProgressViewModal viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected on page, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}