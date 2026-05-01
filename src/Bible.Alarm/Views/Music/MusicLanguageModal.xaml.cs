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
using Serilog;

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
            if (ViewModel is not MusicPublicationSelectionViewModel musicPublicationViewModel ||
                musicPublicationViewModel.SelectLanguageCommand is not IAsyncRelayCommand<LanguageListViewItemModel> asyncCommand ||
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

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var musicPublicationViewModel = ViewModel as MusicPublicationSelectionViewModel;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            new ListModalAppearOptions(
                BusyOverlay,
                LanguageCollectionView,
                GetSelectedItem: () =>
                {
                    var languages = musicPublicationViewModel?.Languages;
                    if (languages == null || languages.Count == 0)
                        return null;
                    return languages.FirstOrDefault(l => l.IsSelected);
                },
                RefreshAction: musicPublicationViewModel != null
                    ? async () => await musicPublicationViewModel.RefreshLanguagesAsync()
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
                            Log.Logger.Debug(ex, "MusicLanguageModal: PopModalAsync failed (modal may already be closed or platform stack out of sync)");
                        }
                    });
                    await toastService.ShowMessage(errorMessage);
                },
                CancellationToken: cancellationTokenSource.Token));
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
