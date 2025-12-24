#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfChaptersModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isClearingSelection;

    public ChaptersSelectionContainerViewModel? ViewModel => BindingContext as ChaptersSelectionContainerViewModel;

    public NumberOfChaptersModal()
    {
        InitializeComponent();

        Log.Information("[NumberOfChaptersModal] Constructor - Initializing");
        Log.Information("[NumberOfChaptersModal] ChaptersCollectionView: {CollectionView}, SelectionMode: {SelectionMode}", 
            ChaptersCollectionView != null, ChaptersCollectionView?.SelectionMode);

        // Handle selection changed - execute command and clear selection
        ChaptersCollectionView.SelectionChanged += OnSelectionChanged;
        Log.Information("[NumberOfChaptersModal] SelectionChanged event handler attached");

        Appearing += OnAppearing;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.Information("[NumberOfChaptersModal] OnSelectionChanged called. isClearingSelection: {IsClearing}, CurrentSelection count: {Count}", 
            isClearingSelection, e?.CurrentSelection?.Count ?? 0);

        // Don't process if we're already clearing or if selection is being cleared
        if (isClearingSelection)
        {
            Log.Information("[NumberOfChaptersModal] Skipping - already clearing selection");
            return;
        }

        if (e?.CurrentSelection == null || e.CurrentSelection.Count == 0)
        {
            Log.Information("[NumberOfChaptersModal] Skipping - no selection or empty selection");
            return;
        }

        // Get the selected item
        var selectedItem = e.CurrentSelection.FirstOrDefault() as NumberOfChaptersListViewItemModel;
        Log.Information("[NumberOfChaptersModal] Selected item: {Item}, ViewModel: {ViewModel}, Command: {Command}", 
            selectedItem?.Value ?? -1, ViewModel != null, ViewModel?.SelectNumberOfChaptersCommand != null);

        if (selectedItem == null)
        {
            Log.Warning("[NumberOfChaptersModal] Selected item is null or not NumberOfChaptersListViewItemModel");
            return;
        }

        if (ViewModel?.SelectNumberOfChaptersCommand == null)
        {
            Log.Warning("[NumberOfChaptersModal] ViewModel or SelectNumberOfChaptersCommand is null");
            return;
        }

        // Execute the command if it can execute
        var canExecute = ViewModel.SelectNumberOfChaptersCommand.CanExecute(selectedItem);
        Log.Information("[NumberOfChaptersModal] Command CanExecute: {CanExecute}", canExecute);

        if (canExecute)
        {
            try
            {
                Log.Information("[NumberOfChaptersModal] Executing command for item: {Value}", selectedItem.Value);
                
                // Cast to AsyncRelayCommand to use ExecuteAsync
                if (ViewModel.SelectNumberOfChaptersCommand is CommunityToolkit.Mvvm.Input.AsyncRelayCommand<NumberOfChaptersListViewItemModel> asyncCommand)
                {
                    Log.Information("[NumberOfChaptersModal] Using ExecuteAsync");
                    await asyncCommand.ExecuteAsync(selectedItem);
                }
                else
                {
                    Log.Information("[NumberOfChaptersModal] Using Execute (not AsyncRelayCommand)");
                    ViewModel.SelectNumberOfChaptersCommand.Execute(selectedItem);
                }
                
                Log.Information("[NumberOfChaptersModal] Command executed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[NumberOfChaptersModal] Error executing command");
            }
        }
        else
        {
            Log.Warning("[NumberOfChaptersModal] Command cannot execute for item: {Value}", selectedItem.Value);
        }

        // Clear selection after a short delay to allow command to execute
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!isDisposed && ChaptersCollectionView != null)
                {
                    Log.Information("[NumberOfChaptersModal] Clearing selection");
                    isClearingSelection = true;
                    ChaptersCollectionView.SelectedItem = null;
                    isClearingSelection = false;
                }
            });
        });
    }

    private async void OnAppearing(object? sender, EventArgs e)
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
