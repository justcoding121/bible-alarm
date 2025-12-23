#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Music;
using Serilog;

namespace Bible.Alarm.Views.Music;

public partial class MusicSelectionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public MusicSelectionViewModel? ViewModel => BindingContext as MusicSelectionViewModel;

    public MusicSelectionModal()
    {
        InitializeComponent();

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel != null)
        {
            // Refresh from state when modal appears to ensure we have the latest music type
            // This is important when the modal is opened after music is enabled or changed
            ViewModel.RefreshFromState();
            
            await Task.Delay(200, cancellationTokenSource.Token);

            if (ViewModel.SelectedMusicType != null && musicTypesCollectionView != null)
            {
                await CollectionViewHelper.ScrollToWhenReadyAsync(musicTypesCollectionView, ViewModel.SelectedMusicType, animated: false, cancellationToken: cancellationTokenSource.Token);
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

