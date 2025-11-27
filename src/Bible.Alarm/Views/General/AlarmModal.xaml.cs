using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.General;

public partial class AlarmModal : ContentPage, IDisposable
{
    private bool _isDisposed;
    private bool _hasHandledFirstLoad;
    private readonly AlarmViewModal _viewModel;

    public AlarmViewModal ViewModel => BindingContext as AlarmViewModal;

    public AlarmModal(AlarmViewModal viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Use Loaded event which fires after the page is in the visual tree
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Only handle once per page instance
        if (_hasHandledFirstLoad) return;
        _hasHandledFirstLoad = true;

        // Unsubscribe to avoid multiple calls
        Loaded -= OnPageLoaded;

        // Wait a bit to ensure the modal is fully rendered and visible
        await Task.Delay(100);
        
        // Hide Home page overlay after Alarm Modal is fully rendered and visible
        _viewModel?.HideHomePageOverlay();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset flag when modal appears again
        _hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
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