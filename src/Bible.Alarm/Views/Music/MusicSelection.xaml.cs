#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicSelection : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly MusicSelectionViewModel viewModel;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicSelectionViewModel? ViewModel => BindingContext as MusicSelectionViewModel;

    public MusicSelection(MusicSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
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

            if (ViewModel.SelectedMusicType != null && musicTypesCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(musicTypesCollectionView, ViewModel.SelectedMusicType, animated: false, cancellationToken: cancellationTokenSource.Token);
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

    private async void OnMusicTypeItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is MusicTypeListItemViewModel musicTypeItem)
        {
            if (ViewModel != null && ViewModel.SongBookSelectionCommand is IAsyncRelayCommand<MusicTypeListItemViewModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(musicTypeItem))
                {
                    await asyncCommand.ExecuteAsync(musicTypeItem);
                }
            }
        }
    }
}
