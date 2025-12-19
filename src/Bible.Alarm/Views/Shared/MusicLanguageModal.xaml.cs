using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Serilog;

namespace Bible.Alarm.Views.Shared;

public partial class MusicLanguageModal : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public IListViewModel ViewModel => BindingContext as IListViewModel;

    private bool isClearingSelection;

    public MusicLanguageModal()
    {
        InitializeComponent();

        // Clear selection after SelectionChanged fires to allow command to execute first
        LanguageCollectionView.SelectionChanged += (sender, e) =>
        {
            // Don't clear if we're already clearing or if selection is being cleared (CurrentSelection is empty or null)
            if (isClearingSelection || e?.CurrentSelection == null || e.CurrentSelection.Count == 0)
            {
                return;
            }

            // Clear selection after a short delay to allow command to execute
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_isDisposed && LanguageCollectionView != null)
                    {
                        isClearingSelection = true;
                        LanguageCollectionView.SelectedItem = null;
                        isClearingSelection = false;
                    }
                });
            });
        };

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // Wait for the page to be fully loaded and data to be ready before attempting to scroll
        if (ViewModel != null)
        {
            // Wait for IsBusy to become false (data loaded) using Polly retry policy
            await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

            // Small additional delay to ensure CollectionView is rendered
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedItem != null && LanguageCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(LanguageCollectionView, ViewModel.SelectedItem, cancellationToken: cancellationTokenSource.Token);
            }
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}

