#nullable enable
using System.Linq;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicLanguageModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingLanguage;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public MusicLanguageModal()
    {
        InitializeComponent();
        var services = Application.Current!.Handler!.MauiContext!.Services;
        navigationService = services.GetRequiredService<INavigationService>();
        toastService = services.GetRequiredService<IToastService>();
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
            catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
            {
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
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

        var result = await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            LanguageCollectionView,
            getSelectedItem: () =>
            {
                var languages = musicPublicationViewModel?.Languages;
                if (languages == null || languages.Count == 0)
                {
                    Serilog.Log.Debug("MusicLanguageModal: getSelectedItem - Languages is null or empty");
                    return null;
                }
                var selected = languages.FirstOrDefault(l => l.IsSelected);
                Serilog.Log.Debug("MusicLanguageModal: getSelectedItem - Languages.Count={Count}, SelectedItem={SelectedCode}",
                    languages.Count, selected?.Code ?? "(null)");
                return selected;
            },
            refreshAction: musicPublicationViewModel != null
                ? async () => await musicPublicationViewModel.RefreshLanguagesAsync()
                : null,
            onFetchFailed: async (errorMessage) =>
            {
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(errorMessage);
            },
            cancellationToken: cancellationTokenSource.Token);

        if (result == ModalAppearingResult.FetchFailed)
        {
            return;
        }
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
