#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Views.Bible;

public partial class BibleSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly BibleSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BibleSelectionViewModel? ViewModel => BindingContext as BibleSelectionViewModel;

    public BibleSelection(BibleSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Note: We don't clear selection here because this page navigates away when an item is selected
        // The page will be disposed, so clearing selection is unnecessary and can interfere with navigation on iOS

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // List is hard-coded, so no need to wait for data loading
        // Just wait a moment for CollectionView to render, then scroll
        if (ViewModel != null)
        {
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedTranslation != null && translationsCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(translationsCollectionView, ViewModel.SelectedTranslation, animated: false, cancellationToken: cancellationTokenSource.Token);
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        viewModel.BackCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!isDisposed)
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
                Serilog.Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}
