#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using Serilog;

namespace Bible.Alarm.Views.Bible;

public partial class BookSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BookSelectionViewModel? ViewModel => BindingContext as BookSelectionViewModel;

    public BookSelectionModal()
    {
        InitializeComponent();

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel != null)
        {
            // Refresh from state when modal appears to ensure we have the latest language/publication
            // This is important when the modal is opened after language or translation changes
            ViewModel.RefreshFromState();
            
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedBook != null && bookCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(bookCollectionView, ViewModel.SelectedBook, animated: false, cancellationToken: cancellationTokenSource.Token);
            }
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            BindingContext = null;
            isDisposed = true;
        }
    }
}

