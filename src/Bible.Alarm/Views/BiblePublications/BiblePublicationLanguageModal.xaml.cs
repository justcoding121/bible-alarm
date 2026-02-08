#nullable enable
using System;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationLanguageModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingLanguage;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public BiblePublicationLanguageModal()
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

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var bibleViewModel = ViewModel as BiblePublicationSelectionViewModel;
        
        // Only clear search term if it's not already empty - avoids triggering unnecessary re-population
        if (bibleViewModel != null && !string.IsNullOrEmpty(bibleViewModel.LanguageSearchTerm))
        {
            try { bibleViewModel.LanguageSearchTerm = string.Empty; } catch { }
        }

        var result = await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            LanguageCollectionView,
            getSelectedItem: () =>
            {
                var languages = bibleViewModel?.Languages;
                if (languages == null || languages.Count == 0)
                {
                    Serilog.Log.Debug("BiblePublicationLanguageModal: getSelectedItem - Languages is null or empty");
                    return null;
                }
                var selected = languages.FirstOrDefault(l => l.IsSelected);
                Serilog.Log.Debug("BiblePublicationLanguageModal: getSelectedItem - Languages.Count={Count}, SelectedItem={SelectedCode}",
                    languages.Count, selected?.Code ?? "(null)");
                return selected;
            },
            refreshAction: bibleViewModel != null
                ? async () => await bibleViewModel.RefreshLanguagesAsync()
                : null,
            onFetchFailed: async (errorMessage) =>
            {
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(errorMessage);
            },
            cancellationToken: cancellationTokenSource.Token);

        // If fetch failed, modal is already closed - nothing more to do
        if (result == ModalAppearingResult.FetchFailed)
        {
            return;
        }

        // Focus the search bar after modal content is loaded for better UX
        FocusSearchBar();
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

        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (isSelectingLanguage)
        {
            return;
        }

        if (sender is View view && view.BindingContext is LanguageListViewItemModel languageItem)
        {
            isSelectingLanguage = true;

            // Set IsNavigating immediately to show progress indicator (progress will be set by command handler only if fetch happens)
            languageItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel is BiblePublicationSelectionViewModel bibleSelectionViewModel)
                {
                    if (bibleSelectionViewModel.SelectLanguageCommand is IAsyncRelayCommand<LanguageListViewItemModel> asyncCommand)
                    {
                        if (asyncCommand.CanExecute(languageItem))
                        {
                            await asyncCommand.ExecuteAsync(languageItem);
                        }
                    }
                }
            }
            catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
            {
                // Fetch failed when user tapped - close modal and show toast
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
            }
            finally
            {
                // Reset IsNavigating after operation completes
                languageItem.IsNavigating = false;
                isSelectingLanguage = false;
            }
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
