#nullable enable
using Bible.Alarm.ViewModels.Shared;

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

    private bool isDragging = false;

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
