#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.BiblePublications;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public sealed partial class BiblePublicationSectionSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingSection;
    private readonly BiblePublicationSectionSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BiblePublicationSectionSelectionViewModel? ViewModel => viewModel;

    public BiblePublicationSectionSelection(BiblePublicationSectionSelectionViewModel viewModel, TaskScheduler taskScheduler)
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

        // Wait for the page to be fully loaded and data to be ready before attempting to scroll
        if (ViewModel != null)
        {
            // Wait for IsBusy to become false (data loaded) using Polly retry policy
            await CollectionViewHelper.WaitForNotBusyAsync(() => ViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

            // Small additional delay to ensure CollectionView is rendered
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedSection != null && sectionCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(sectionCollectionView, ViewModel.SelectedSection, cancellationToken: cancellationTokenSource.Token);
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel?.BackCommand?.Execute(null);
        return true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            // Cancel and dispose cancellation token source
            try
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
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
        }

        isDisposed = true;
    }

    private async void OnSectionItemTapped(object? sender, TappedEventArgs e)
    {
        if (isSelectingSection)
        {
            return;
        }

        if (sender is not View view || view.BindingContext is not BiblePublicationSectionListViewItemModel sectionItem)
        {
            return;
        }

        isSelectingSection = true;

        // Reset progress and show row indicator immediately
        sectionItem.DownloadProgress = 0.0;
        sectionItem.IsNavigating = true;

        // Wait 50ms to ensure UI thread renders the update before doing backend work
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.TrackSelectionCommand is not IAsyncRelayCommand<BiblePublicationSectionListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(sectionItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(sectionItem);
        }
        finally
        {
            // Reset IsNavigating after operation completes
            sectionItem.IsNavigating = false;
            isSelectingSection = false;
        }
    }
}
