#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicLanguageModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public MusicLanguageModal()
    {
        InitializeComponent();

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        Appearing += OnAppearing;
    }

    // Force busy overlay to hide when needed
    private void ForceHideBusyOverlay()
    {
        if (BusyOverlay != null)
        {
            BusyOverlay.IsVisible = false;
        }
    }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is LanguageListViewItemModel languageItem)
        {
            if (ViewModel is SongBookSelectionViewModel songBookViewModel)
            {
                if (songBookViewModel.SelectLanguageCommand is IAsyncRelayCommand<LanguageListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(languageItem))
                    {
                        await asyncCommand.ExecuteAsync(languageItem);
                    }
                }
            }
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        // Wait for the page to be fully loaded and data to be ready before attempting to scroll
        if (ViewModel != null)
        {
            // If ViewModel is SongBookSelectionViewModel, refresh from state to ensure languages are populated
            if (ViewModel is SongBookSelectionViewModel songBookViewModel)
            {
                await songBookViewModel.RefreshFromState();
            }

            // Force hide busy overlay since binding might not work
            ForceHideBusyOverlay();

            if (ViewModel.SelectedItem != null && LanguageCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(LanguageCollectionView, ViewModel.SelectedItem, cancellationToken: cancellationTokenSource.Token);
            }
        }
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
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}

