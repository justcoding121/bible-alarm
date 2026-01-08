#nullable enable
using System;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Serilog;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BibleLanguageModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public BibleLanguageModal()
    {
        InitializeComponent();

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        Appearing += OnAppearing;
    }

    // Force busy overlay to hide when needed
    private void ForceHideBusyOverlay()
    {
        if (BusyOverlay != null)
        {
            BusyOverlay.IsVisible = false;
        }
    }

        private async void OnAppearing(object? sender, EventArgs e)
        {
            Appearing -= OnAppearing;

            try
            {
                if (ViewModel != null)
                {
                    var bibleViewModel = ViewModel as BibleSelectionViewModel;
                    if (bibleViewModel != null)
                    {
                        // Ensure initialization is complete and busy is turned off first
                        await bibleViewModel.RefreshFromState();

                        // Force hide busy overlay since binding might not work
                        ForceHideBusyOverlay();

                        // Clear search term to show all languages when modal opens
                        // This ensures the selected language is visible and scrollable
                        try
                        {
                            bibleViewModel.LanguageSearchTerm = string.Empty;
                        }
                        catch
                        {
                            // Ignore errors when clearing search term
                        }

                        // Wait for IsBusy to become false (data loaded) using Polly retry policy
                        // This ensures the CollectionView is ready and languages are populated before scrolling
                        await CollectionViewHelper.WaitForNotBusyAsync(() => bibleViewModel.IsBusy, cancellationToken: cancellationTokenSource.Token);

                        // Small additional delay to ensure CollectionView is rendered
                        await Task.Delay(200, cancellationTokenSource.Token);

                        // Find the currently selected language in the collection
                        var selectedLanguage = bibleViewModel.Languages?.FirstOrDefault(l => l.IsSelected);
                        if (selectedLanguage != null && LanguageCollectionView != null)
                        {
                            await CollectionViewHelper.ScrollToWhenReadyAsync(LanguageCollectionView, selectedLanguage, animated: false, cancellationToken: cancellationTokenSource.Token);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // OnAppearing errors are non-critical (UI initialization)
                Serilog.Log.Warning(ex, "Error in BibleLanguageModal.OnAppearing");
                // Force hide busy overlay on error
                ForceHideBusyOverlay();
            }
        }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is LanguageListViewItemModel languageItem)
        {
            if (ViewModel is BibleSelectionViewModel bibleSelectionViewModel)
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

            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}

