#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

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
        Appearing += OnAppearing;
    }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

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

        var songBookViewModel = ViewModel as SongBookSelectionViewModel;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            LanguageCollectionView,
            // Get selected item AFTER refresh to ensure fresh reference
            getSelectedItem: () => songBookViewModel?.Languages?.FirstOrDefault(l => l.IsSelected),
            refreshAction: songBookViewModel != null 
                ? async () => await songBookViewModel.RefreshFromState() 
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
}

