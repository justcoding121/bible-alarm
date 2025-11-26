using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Views;

public partial class Home : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly HomeViewModel _viewModel;

    public Home(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _viewModel = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        // Prevent back navigation on Home page - it's the root page
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