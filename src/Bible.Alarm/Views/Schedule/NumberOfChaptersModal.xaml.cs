using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfChaptersModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isClearingSelection;

    public ScheduleViewModel ViewModel => BindingContext as ScheduleViewModel;

    public NumberOfChaptersModal()
    {
        InitializeComponent();

        // Clear selection after SelectionChanged fires to allow command to execute first
        ChaptersCollectionView.SelectionChanged += (sender, e) =>
        {
            // Don't clear if we're already clearing or if selection is being cleared (CurrentSelection is empty or null)
            if (isClearingSelection || e?.CurrentSelection == null || e.CurrentSelection.Count == 0)
            {
                return;
            }

            // Clear selection after a short delay to allow command to execute
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!isDisposed && ChaptersCollectionView != null)
                    {
                        isClearingSelection = true;
                        ChaptersCollectionView.SelectedItem = null;
                        isClearingSelection = false;
                    }
                });
            });
        };

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;

        if (ViewModel?.CurrentNumberOfChapters != null && ChaptersCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(ChaptersCollectionView, ViewModel.CurrentNumberOfChapters, cancellationToken: cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                Log.Logger.Warning(ex, "Error during cancellation token source disposal");
            }

            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}
