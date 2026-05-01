#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicPublicationSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingPublication;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public MusicPublicationSelectionViewModel? ViewModel => BindingContext as MusicPublicationSelectionViewModel;

    public MusicPublicationSelectionModal()
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
            new ListModalAppearOptions(
                BusyOverlay,
                songPublicationsCollectionView,
                GetSelectedItem: () => ViewModel?.SelectedSongPublication,
                RefreshAction: ViewModel != null
                    ? async () => await ViewModel.RefreshFromState()
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
                            Log.Logger.Debug(ex, "MusicPublicationSelectionModal: PopModalAsync failed (modal may already be closed or platform stack out of sync)");
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

    private async void OnSongPublicationItemTapped(object? sender, TappedEventArgs e)
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

        publicationItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.TrackSelectionCommand is not IAsyncRelayCommand<PublicationListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(publicationItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(publicationItem);
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
