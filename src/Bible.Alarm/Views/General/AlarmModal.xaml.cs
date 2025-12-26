#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using Serilog;

namespace Bible.Alarm.Views.General;

public partial class AlarmModal : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool hasHandledFirstLoad;
    private readonly AlarmViewModal viewModel;

    public AlarmViewModal? ViewModel => BindingContext as AlarmViewModal;

    public AlarmModal(AlarmViewModal viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Use Loaded event which fires after the page is in the visual tree
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Only handle once per page instance
        if (hasHandledFirstLoad)
        {
            return;
        }

        hasHandledFirstLoad = true;

        // Unsubscribe to avoid multiple calls
        Loaded -= OnPageLoaded;

        // Wait a bit to ensure the modal is fully rendered and visible
        await Task.Delay(100);

        // Hide Home page overlay after Alarm Modal is fully rendered and visible
        viewModel?.HideHomePageOverlay();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset flag when modal appears again
        hasHandledFirstLoad = false;
        Loaded += OnPageLoaded;
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel?.DismissCommand.Execute(null);
        return true;
    }

    private async void OnStopButtonClicked(object? sender, EventArgs e)
    {
        // Fallback handler to ensure stop button always works
        // This ensures the command executes even if binding fails or command is blocked
        var logger = Log.Logger;
        logger.Information("Stop button clicked - OnStopButtonClicked handler fired");
        
        try
        {
            // Try command first
            if (ViewModel?.DismissCommand != null && ViewModel.DismissCommand.CanExecute(null))
            {
                logger.Debug("Executing DismissCommand via click handler");
                ViewModel.DismissCommand.Execute(null);
            }
            else
            {
                logger.Warning("DismissCommand is null or cannot execute, attempting direct stop");
                // If command fails, try direct stop
                var playbackService = ServiceProviderManager.GetService<IPlaybackService>();
                if (playbackService != null)
                {
                    await playbackService.StopAsync();
                }
                else
                {
                    logger.Error("PlaybackService not available");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in OnStopButtonClicked, attempting direct stop");
            // If command execution fails, try direct stop
            try
            {
                var playbackService = ServiceProviderManager.GetService<IPlaybackService>();
                if (playbackService != null)
                {
                    await playbackService.StopAsync();
                }
            }
            catch (Exception stopEx)
            {
                logger.Error(stopEx, "Direct stop also failed");
            }
        }
    }

    private bool isDragging;

    private void OnSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        // Ignore programmatic updates (from binding)
        if (!ViewModel.IsUserInteracting)
        {
            return;
        }

        // If user is dragging, ignore ValueChanged (drag will be handled by DragCompleted)
        if (isDragging)
        {
            return;
        }

        // This is a tap (ValueChanged without drag) - handle it
        ViewModel.OnSliderTapped(e.NewValue);
    }

    private void OnSliderDragStarted(object? sender, EventArgs e)
    {
        isDragging = true;
        ViewModel?.OnSliderDragStarted();
    }

    private void OnSliderDragCompleted(object? sender, EventArgs e)
    {
        isDragging = false;
        if (sender is Slider slider && ViewModel != null)
        {
            ViewModel.OnSliderDragCompleted(slider.Value);
        }
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}
