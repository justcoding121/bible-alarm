#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Music;

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
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is LanguageListViewItemModel languageItem)
        {
            languageItem.IsNavigating = true;
            await Task.Delay(50);

            try
            {
                if (ViewModel is MusicPublicationSelectionViewModel musicPublicationViewModel)
                {
                    if (musicPublicationViewModel.SelectLanguageCommand is IAsyncRelayCommand<LanguageListViewItemModel> asyncCommand)
                    {
                        if (asyncCommand.CanExecute(languageItem))
                            await asyncCommand.ExecuteAsync(languageItem);
                    }
                }
            }
            finally
            {
                languageItem.IsNavigating = false;
            }
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        try
        {
            await Task.Yield();

            var musicPublicationViewModel = ViewModel as MusicPublicationSelectionViewModel;

            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                LanguageCollectionView.Opacity = 0;
            }

            await Task.Yield();

            if (musicPublicationViewModel != null)
            {
                await musicPublicationViewModel.RefreshFromState();
            }

            await CollectionViewHelper.WaitForNotBusyAsync(
                () => ViewModel?.IsBusy ?? false,
                cancellationToken: cancellationTokenSource.Token);

            await Task.Delay(150, cancellationTokenSource.Token);

            var selectedItem = musicPublicationViewModel?.Languages?.FirstOrDefault(l => l.IsSelected);
            if (selectedItem != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(
                    LanguageCollectionView,
                    selectedItem,
                    animated: false,
                    cancellationToken: cancellationTokenSource.Token);

                await Task.Delay(50, cancellationTokenSource.Token);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BusyOverlay.IsVisible = false;
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    LanguageCollectionView.Opacity = 1;
                }
            });
        }
        catch (OperationCanceledException)
        {
            BusyOverlay.IsVisible = false;
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            {
                LanguageCollectionView.Opacity = 1;
            }
        }
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
