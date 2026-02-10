#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingPublication;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public BiblePublicationSelectionViewModel? ViewModel => BindingContext as BiblePublicationSelectionViewModel;

    public BiblePublicationSelectionModal()
    {
        InitializeComponent();
        var services = Application.Current!.Handler!.MauiContext!.Services;
        navigationService = services.GetRequiredService<INavigationService>();
        toastService = services.GetRequiredService<IToastService>();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        var result = await ModalScrollHelper.HandleModalAppearingAsync(
            ViewModel,
            BusyOverlay,
            publicationsCollectionView,
            getSelectedItem: () => ViewModel?.SelectedPublication,
            refreshAction: ViewModel != null
                ? async () => await ViewModel.RefreshFromState()
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

    public void Dispose()
    {
        if (!isDisposed)
        {
            ModalScrollHelper.DisposeModal(cancellationTokenSource, () => BindingContext = null, ViewModel);
            isDisposed = true;
        }
    }

    private async void OnPublicationItemTapped(object? sender, TappedEventArgs e)
    {
        // Cancel any ongoing scroll operation to prevent race conditions
        try { cancellationTokenSource.Cancel(); } catch { }

        if (isSelectingPublication)
        {
            return;
        }

        if (sender is View view && view.BindingContext is PublicationListViewItemModel publicationItem)
        {
            isSelectingPublication = true;

            // Ensure overlay is hidden when user selects a publication
            // This prevents overlay from staying visible if cancellation happens before HandleModalAppearingAsync completes
            if (ViewModel != null && ViewModel.IsBusy)
            {
                ViewModel.IsBusy = false;
            }

            // Show row indicator immediately (progress will be set by command handler only if fetch happens)
            publicationItem.IsNavigating = true;
            
            // Wait 50ms to ensure UI thread renders the update before doing backend work
            await Task.Delay(50);

            try
            {
                if (ViewModel != null && ViewModel.SectionSelectionCommand is IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand)
                {
                    if (asyncCommand.CanExecute(publicationItem))
                    {
                        await asyncCommand.ExecuteAsync(publicationItem);
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
                // Reset IsNavigating after operation completes
                publicationItem.IsNavigating = false;
                isSelectingPublication = false;
            }
        }
    }
}

