#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Views.Bible;

public partial class BibleSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BibleSelectionViewModel? ViewModel => BindingContext as BibleSelectionViewModel;

    public BibleSelectionModal()
    {
        InitializeComponent();

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel != null)
        {
            // Refresh from state when modal appears to ensure translations are populated
            await ViewModel.RefreshFromState();

            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedTranslation != null && translationsCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(translationsCollectionView, ViewModel.SelectedTranslation, animated: false, cancellationToken: cancellationTokenSource.Token);
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

    private async void OnTranslationItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is PublicationListViewItemModel publicationItem)
        {
            if (ViewModel != null && ViewModel.BookSelectionCommand is IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(publicationItem))
                {
                    await asyncCommand.ExecuteAsync(publicationItem);
                }
            }
        }
    }
}

