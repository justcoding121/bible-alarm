#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BibleSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public BibleSelectionViewModel? ViewModel => BindingContext as BibleSelectionViewModel;

    public BibleSelectionModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            () => ViewModel?.IsBusy ?? false,
            BusyOverlay,
            translationsCollectionView,
            getSelectedItem: () => ViewModel?.SelectedTranslation,
            refreshAction: ViewModel != null 
                ? async () => await ViewModel.RefreshFromState() 
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

    private async void OnTranslationItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (sender is Grid grid && grid.BindingContext is PublicationListViewItemModel publicationItem)
        {
            if (ViewModel != null && ViewModel.SectionSelectionCommand is IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(publicationItem))
                {
                    await asyncCommand.ExecuteAsync(publicationItem);
                }
            }
        }
    }
}

