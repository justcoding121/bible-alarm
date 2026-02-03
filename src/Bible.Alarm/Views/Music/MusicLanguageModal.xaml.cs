#nullable enable
using System.Linq;
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
    private bool isSelectingLanguage;
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

        if (isSelectingLanguage)
        {
            return;
        }

        if (sender is View view && view.BindingContext is LanguageListViewItemModel languageItem)
        {
            isSelectingLanguage = true;

            languageItem.IsNavigating = true;
            languageItem.DownloadProgress = 0.0;
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
                isSelectingLanguage = false;
            }
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var musicPublicationViewModel = ViewModel as MusicPublicationSelectionViewModel;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            LanguageCollectionView,
            getSelectedItem: () => musicPublicationViewModel?.Languages?.FirstOrDefault(l => l.IsSelected),
            refreshAction: musicPublicationViewModel != null
                ? async () => await musicPublicationViewModel.RefreshFromState()
                : null,
            cancellationToken: cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
            isDisposed = true;
        }
    }
}
