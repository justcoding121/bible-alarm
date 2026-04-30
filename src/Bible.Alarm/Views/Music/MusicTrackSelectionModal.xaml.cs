#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Music;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Views.Music;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicTrackSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool isSelectingTrack;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly INavigationService navigationService;
    private readonly IToastService toastService;

    public MusicTrackSelectionViewModel? ViewModel => BindingContext as MusicTrackSelectionViewModel;

    public MusicTrackSelectionModal()
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
            refreshAction: ViewModel != null ? async () => await ViewModel.RefreshFromState() : null,
            onFetchFailed: async (errorMessage) =>
            {
                await navigationService.PopModalAsync();
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

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not MusicTrackListViewItemModel trackItem)
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
                ViewModel.SetTrackCommand is not IAsyncRelayCommand<MusicTrackListViewItemModel> asyncCommand ||
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
