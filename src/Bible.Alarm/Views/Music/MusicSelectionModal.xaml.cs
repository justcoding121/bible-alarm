#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicSelectionViewModel? ViewModel => BindingContext as MusicSelectionViewModel;

    public MusicSelectionModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            () => ViewModel?.IsBusy ?? false,
            BusyOverlay,
            musicTypesCollectionView,
            getSelectedItem: () => ViewModel?.SelectedMusicType,
            refreshAction: ViewModel != null 
                ? () => { ViewModel.RefreshFromState(); return Task.CompletedTask; }
                : null,
            cancellationToken: cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null);
            isDisposed = true;
        }
    }

    private async void OnMusicTypeItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

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

