#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Views.Bible;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BiblePublicationTrackSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingTrack;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public BiblePublicationTrackSelectionViewModel? ViewModel => BindingContext as BiblePublicationTrackSelectionViewModel;

    public BiblePublicationTrackSelectionModal()
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
            trackCollectionView,
            getSelectedItem: () => ViewModel?.SelectedTrack,
            refreshAction: ViewModel != null
                ? async () => await ViewModel.RefreshFromState()
                : null,
            onFetchFailed: async (errorMessage) =>
            {
                await navigationService.PopModalAsync();
                await toastService.ShowMessage(errorMessage);
            },
            cancellationToken: cancellationTokenSource.Token);
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

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not BiblePublicationTrackListViewItemModel trackItem)
        {
            return;
        }

        await SafeTeardown.CancelAsyncNoThrow(cancellationTokenSource);

        if (isSelectingTrack)
        {
            return;
        }

        isSelectingTrack = true;

        trackItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel is null ||
                ViewModel.SetTrackCommand is not IAsyncRelayCommand<BiblePublicationTrackListViewItemModel> asyncCommand ||
                !asyncCommand.CanExecute(trackItem))
            {
                return;
            }

            await asyncCommand.ExecuteAsync(trackItem);
        }
        catch (Exception ex) when (ModalScrollHelper.IsFetchFailure(ex))
        {
            await navigationService.PopModalAsync();
            await toastService.ShowMessage(ModalScrollHelper.GetFetchErrorMessage(ex));
        }
        finally
        {
            trackItem.IsNavigating = false;
            isSelectingTrack = false;
        }
    }
}
