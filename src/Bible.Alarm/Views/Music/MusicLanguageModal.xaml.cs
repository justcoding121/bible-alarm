#nullable enable
using System;
using System.Linq;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

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
        WireUpSearchBarHandlers();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Wire up handlers after visual tree is ready
        if (Handler != null)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                WireUpSearchBarHandlers();
            });
        }
    }

    private void WireUpSearchBarHandlers()
    {
        if (LanguageSearchBarIOS != null)
        {
            LanguageSearchBarIOS.Unfocused -= OnSearchBarUnfocused;
            LanguageSearchBarIOS.Unfocused += OnSearchBarUnfocused;
        }
        
        if (LanguageSearchBarNonIOS != null)
        {
            LanguageSearchBarNonIOS.Unfocused -= OnSearchBarUnfocused;
            LanguageSearchBarNonIOS.Unfocused += OnSearchBarUnfocused;
        }
    }

    private void OnSearchBarUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is SearchBar searchBar && !searchBar.IsFocused)
        {
            KeyboardHelper.HideKeyboard(searchBar);
        }
    }

    private void OnGridTapped(object? sender, TappedEventArgs e)
    {
        UnfocusAnyFocusedSearchBar();
    }

    private void UnfocusAnyFocusedSearchBar()
    {
        if (LanguageSearchBarIOS?.IsFocused == true)
        {
            KeyboardHelper.HideKeyboard(LanguageSearchBarIOS);
        }
        else if (LanguageSearchBarNonIOS?.IsFocused == true)
        {
            KeyboardHelper.HideKeyboard(LanguageSearchBarNonIOS);
        }
    }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        // Hide keyboard when list item is tapped
        UnfocusAnyFocusedSearchBar();

        try { cancellationTokenSource.Cancel(); } catch { }

        if (isSelectingLanguage)
        {
            return;
        }

        if (sender is View view && view.BindingContext is LanguageListViewItemModel languageItem)
        {
            isSelectingLanguage = true;

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
            catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
            {
                await Task.Delay(500);
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
                    return null;
                return languages.FirstOrDefault(l => l.IsSelected);
            },
            refreshAction: musicPublicationViewModel != null
                ? async () => await musicPublicationViewModel.RefreshLanguagesAsync()
                : null,
            onFetchFailed: async (errorMessage) =>
            {
                await this.Dispatcher.DispatchAsync(async () =>
                {
                    try
                    {
                        await Task.Delay(500);
                        await navigationService.PopModalAsync();
                    }
                    catch (InvalidOperationException)
                    {
                        // Modal may already be closed or platform stack out of sync.
                    }
                });
                await toastService.ShowMessage(errorMessage);
            },
            cancellationToken: cancellationTokenSource.Token);

        if (result == ModalAppearingResult.FetchFailed)
        {
            return;
        }
    }

    private void FocusSearchBar()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var searchBar = DeviceInfo.Platform == DevicePlatform.iOS
                ? LanguageSearchBarIOS
                : LanguageSearchBarNonIOS;

            if (searchBar != null)
            {
                searchBar.Focus();
            }
        });
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
