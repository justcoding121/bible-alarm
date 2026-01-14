#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationLanguageModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public IListViewModel? ViewModel => BindingContext as IListViewModel;

    public BiblePublicationLanguageModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var bibleViewModel = ViewModel as BiblePublicationSelectionViewModel;

        // Clear search term to show all languages when modal opens
        if (bibleViewModel != null)
        {
            try { bibleViewModel.LanguageSearchTerm = string.Empty; } catch { }
        }

        await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            LanguageCollectionView,
            // Get selected item AFTER refresh to ensure fresh reference
            getSelectedItem: () => bibleViewModel?.Languages?.FirstOrDefault(l => l.IsSelected),
            refreshAction: bibleViewModel != null
                ? async () => await bibleViewModel.RefreshFromState()
                : null,
            cancellationToken: cancellationTokenSource.Token);
    }

    private async void OnLanguageItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is View view && view.BindingContext is LanguageListViewItemModel languageItem)
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

