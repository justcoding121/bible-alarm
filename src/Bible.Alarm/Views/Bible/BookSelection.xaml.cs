using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using Serilog;

namespace Bible.Alarm.Views.Bible;

public partial class BookSelection : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly BookSelectionViewModel _viewModel;
    private readonly TaskScheduler _taskScheduler;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public BookSelectionViewModel ViewModel => BindingContext as BookSelectionViewModel;

    public BookSelection(BookSelectionViewModel viewModel, TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Note: We don't clear selection here because this page navigates away when an item is selected
        // The page will be disposed, so clearing selection is unnecessary and can interfere with navigation on iOS

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;
        
        // Wait for the page to be fully loaded and data to be ready before attempting to scroll
        if (ViewModel != null)
        {
            // Wait for IsBusy to become false (data loaded) using Polly retry policy
            await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: _cancellationTokenSource.Token);
            
            // Small additional delay to ensure CollectionView is rendered
            await Task.Delay(200, _cancellationTokenSource.Token);
            
            if (ViewModel.SelectedBook != null && bookCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(bookCollectionView, ViewModel.SelectedBook, cancellationToken: _cancellationTokenSource.Token);
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }
            
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}