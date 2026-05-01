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
using Serilog;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public sealed partial class BiblePublicationLanguageModal : BaseContentPage, IDisposable
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
        WireUpSearchEntryHandlers();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Wire up handlers after visual tree is ready
        if (Handler != null)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                WireUpSearchEntryHandlers();
            });
        }
    }

    private void WireUpSearchEntryHandlers()
    {
        if (LanguageSearchEntry != null)
        {
            LanguageSearchEntry.Unfocused -= OnSearchEntryUnfocused;
            LanguageSearchEntry.Unfocused += OnSearchEntryUnfocused;
        }
    }

    private static void OnSearchEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && !entry.IsFocused)
        {
            KeyboardHelper.HideKeyboard(entry);
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var bibleViewModel = ViewModel as BiblePublicationSelectionViewModel;
        
        // Only clear search term if it's not already empty - avoids triggering unnecessary re-population
        if (bibleViewModel != null && !string.IsNullOrEmpty(bibleViewModel.LanguageSearchTerm))
        {
            try
            {
                bibleViewModel.LanguageSearchTerm = string.Empty;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "BiblePublicationLanguageModal: could not clear language search term");
            }
        }

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            new ListModalAppearOptions(
                BusyOverlay,
                LanguageCollectionView,
                GetSelectedItem: () =>
                {
                    var languages = bibleViewModel?.Languages;
                    if (languages == null || languages.Count == 0)
                        return null;
                    return languages.FirstOrDefault(l => l.IsSelected);
                },
                RefreshAction: bibleViewModel != null
                    ? async () => await bibleViewModel.RefreshLanguagesAsync()
                    : null,
                OnFetchFailed: async (errorMessage) =>
                {
                    await this.Dispatcher.DispatchAsync(async () =>
                    {
                        try
                        {
                            await Task.Delay(500);
                            await navigationService.PopModalAsync();
                        }
                        catch (InvalidOperationException ex)
                        {
                            Log.Logger.Debug(ex, "BiblePublicationLanguageModal: PopModalAsync failed (modal may already be closed or platform stack out of sync)");
                        }
                    });
                    await toastService.ShowMessage(errorMessage);
                },
                CancellationToken: cancellationTokenSource.Token));
    }

    private void OnGridTapped(object? sender, TappedEventArgs e)
    {
        UnfocusSearchEntry();
    }

    private void UnfocusSearchEntry()
    {
        if (LanguageSearchEntry?.IsFocused is true)
        {
            KeyboardHelper.HideKeyboard(LanguageSearchEntry);
        }
    }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not LanguageListViewItemModel languageItem)
        {
            return;
        }

        UnfocusSearchEntry();

        await SafeTeardown.CancelAsyncNoThrow(cancellationTokenSource);

        if (isSelectingLanguage)
        {
            return;
        }

        isSelectingLanguage = true;

        languageItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel is not BiblePublicationSelectionViewModel bibleSelectionViewModel ||
                bibleSelectionViewModel.SelectLanguageCommand is not IAsyncRelayCommand<LanguageListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(languageItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(languageItem);
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

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
        }

        isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
