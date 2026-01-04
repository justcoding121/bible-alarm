#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
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

    // Force busy overlay to hide when needed
    private void ForceHideBusyOverlay()
    {
        if (BusyOverlay != null)
        {
            // Ensure this runs on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BusyOverlay.IsVisible = false;
                // Also ensure InputTransparent is set to allow taps through
                BusyOverlay.InputTransparent = true;
                Serilog.Log.Debug("BibleSelectionModal: ForceHideBusyOverlay - Set IsVisible=false, InputTransparent=true");
            });
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        try
        {
            if (ViewModel != null)
            {
                // Refresh from state when modal appears to ensure translations are populated
                await ViewModel.RefreshFromState();

                // Force hide busy overlay since binding might not work
                ForceHideBusyOverlay();

                if (ViewModel.SelectedTranslation != null && translationsCollectionView != null)
                {
                    await CollectionViewHelper.ScrollToWhenReadyAsync(translationsCollectionView, ViewModel.SelectedTranslation, animated: false, cancellationToken: cancellationTokenSource.Token);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error in BibleSelectionModal.OnAppearing");
            // Force hide busy overlay on error
            ForceHideBusyOverlay();
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

