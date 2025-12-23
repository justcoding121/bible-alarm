#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using Serilog;

namespace Bible.Alarm.Views.Music;

public partial class TrackSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public TrackSelectionViewModel? ViewModel => BindingContext as TrackSelectionViewModel;

    public TrackSelectionModal()
    {
        InitializeComponent();

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel != null)
        {
            // Refresh from state when modal appears to ensure tracks are populated
            await ViewModel.RefreshFromState();
            
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedTrack != null && trackCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(trackCollectionView, ViewModel.SelectedTrack, animated: false, cancellationToken: cancellationTokenSource.Token);
            }
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            BindingContext = null;
            isDisposed = true;
        }
    }
}

