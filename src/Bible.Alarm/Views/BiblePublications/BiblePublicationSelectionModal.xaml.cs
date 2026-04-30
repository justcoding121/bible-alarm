#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

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

        await ModalScrollHelper.HandleModalAppearingAsync(
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
                    catch (InvalidOperationException ex)
                    {
                        Log.Logger.Debug(ex, "BiblePublicationSelectionModal: PopModalAsync failed (modal may already be closed or platform stack out of sync)");
                    }
                });
                await toastService.ShowMessage(errorMessage);
            },
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

    private async void OnPublicationItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not PublicationListViewItemModel publicationItem)
        {
            return;
        }

        await SafeTeardown.CancelAsyncNoThrow(cancellationTokenSource);

        if (isSelectingPublication)
        {
            return;
        }

        isSelectingPublication = true;

        if (ViewModel != null && ViewModel.IsBusy)
        {
            ViewModel.IsBusy = false;
        }

        publicationItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel != null
                && ViewModel.SectionSelectionCommand is IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand
                && asyncCommand.CanExecute(publicationItem))
            {
                await asyncCommand.ExecuteAsync(publicationItem);
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
            publicationItem.IsNavigating = false;
            isSelectingPublication = false;
        }
    }
}

