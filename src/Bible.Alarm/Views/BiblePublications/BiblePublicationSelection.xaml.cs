#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingPublication;
    private readonly BiblePublicationSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BiblePublicationSelectionViewModel? ViewModel => BindingContext as BiblePublicationSelectionViewModel;

    public BiblePublicationSelection(BiblePublicationSelectionViewModel viewModel)
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

            if (ViewModel.SelectedPublication != null && publicationsCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(publicationsCollectionView, ViewModel.SelectedPublication, animated: false, cancellationToken: cancellationTokenSource.Token);
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
                Log.Logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
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

    private async void OnPublicationItemTapped(object? sender, TappedEventArgs e)
    {
        if (isSelectingPublication)
        {
            return;
        }

        if (sender is not View view || view.BindingContext is not PublicationListViewItemModel publicationItem)
        {
            return;
        }

        isSelectingPublication = true;

        // Reset progress and show row indicator immediately
        publicationItem.DownloadProgress = 0.0;
        publicationItem.IsNavigating = true;

        // Wait 50ms to ensure UI thread renders the update before doing backend work
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.SectionSelectionCommand is not IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(publicationItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(publicationItem);
        }
        finally
        {
            // Reset IsNavigating after operation completes
            publicationItem.IsNavigating = false;
            isSelectingPublication = false;
        }
    }
}
