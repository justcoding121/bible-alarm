#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfTracksModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public NumberOfTrackContainerViewModel? ViewModel => BindingContext as NumberOfTrackContainerViewModel;
    public IListViewModel? ListViewModel => ViewModel;

    public NumberOfTracksModal()
    {
        InitializeComponent();

        // Apply platform-specific styling in code-behind for better performance
        // This avoids expensive OnPlatform markup extension evaluation at runtime
        ApplyPlatformSpecificStyling();

        // SelectionChanged handler removed - using SelectionMode="None" with TapGestureRecognizer instead
        Appearing += OnAppearing;
    }

    private void ApplyPlatformSpecificStyling()
    {
        var platform = DeviceInfo.Platform;

    }

    private async void OnTrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || view.BindingContext is not NumberOfTracksListViewItemModel trackItem)
        {
            return;
        }

        try { await cancellationTokenSource.CancelAsync(); } catch { }

        trackItem.IsNavigating = true;
        await Task.Delay(50);

        try
        {
            if (ViewModel != null && ViewModel.SelectNumberOfTracksCommand is IAsyncRelayCommand<NumberOfTracksListViewItemModel> asyncCommand)
            {
                if (asyncCommand.CanExecute(trackItem))
                {
                    await asyncCommand.ExecuteAsync(trackItem);
                }
            }
        }
        finally
        {
            trackItem.IsNavigating = false;
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        await ModalScrollHelper.HandleModalAppearingAsync(
            ListViewModel,
            BusyOverlay,
            TracksCollectionView,
            getSelectedItem: () => ViewModel?.CurrentNumberOfTracks,
            refreshAction: ViewModel != null
                ? () => ViewModel.PopulateNumberOfTracksListViewAsync()
                : null,
            onFetchFailed: null,
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
}
