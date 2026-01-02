#nullable enable

using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;
using Microsoft.Maui.Controls.Xaml;
using Serilog;
using System.ComponentModel;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MusicSelectionContainer : ContentView, IDisposable
{
    private MusicSelectionContainerViewModel? viewModel;
    private bool isDisposed;

    // Helper classes
    private AnimationManager? animationManager;
    private PropertyChangeHandler? propertyChangeHandler;
    private VisibilityManager? visibilityManager;
    private ScrollManager? scrollManager;

    public MusicSelectionContainer()
    {
        InitializeComponent();
    }

    public MusicSelectionContainer(MusicSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
        this.viewModel = viewModel;
        InitializeHelpers();

        if (viewModel != null && propertyChangeHandler != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
            propertyChangeHandler.ShouldScrollOnExpand = false; // Don't scroll on initial load
            propertyChangeHandler.IsInitialLoad = true; // Mark as initial load
            // Don't set initial state here - wait for OnHandlerChanged when CollapsibleContent is ready
            // Mark initial load as complete after a short delay to allow any property changes to settle
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                if (propertyChangeHandler != null)
                {
                    propertyChangeHandler.IsInitialLoad = false;
                }
            });
        }
    }

    private void InitializeHelpers()
    {
        if (CollapsibleContent == null) return;

        animationManager = new AnimationManager(this, CollapsibleContent);
        scrollManager = new ScrollManager(this);
        visibilityManager = new VisibilityManager(
            CollapsibleContent,
            animationManager,
            () =>
            {
                if (propertyChangeHandler?.ShouldScrollOnExpand == true)
                {
                    scrollManager?.ScrollToExpandedContent();
                    if (propertyChangeHandler != null)
                    {
                        propertyChangeHandler.ShouldScrollOnExpand = false;
                    }
                }
            });
        propertyChangeHandler = new PropertyChangeHandler(
            this,
            (isEnabled, animate) => visibilityManager?.UpdateCollapsibleContentVisibility(isEnabled, animate),
            () => scrollManager?.ScrollToExpandedContent());
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Unsubscribe from old view model
        if (viewModel != null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new view model
        viewModel = BindingContext as MusicSelectionContainerViewModel;
        if (viewModel != null && propertyChangeHandler != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
            propertyChangeHandler.ShouldScrollOnExpand = false; // Don't scroll on initial load
            propertyChangeHandler.IsInitialLoad = true; // Mark as initial load
            // Don't set initial state here - wait for OnHandlerChanged when CollapsibleContent is ready
            // Mark initial load as complete after a short delay to allow any property changes to settle
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                if (propertyChangeHandler != null)
                {
                    propertyChangeHandler.IsInitialLoad = false;
                }
            });
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        propertyChangeHandler?.OnViewModelPropertyChanged(sender, e, viewModel);
    }


    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler == null || CollapsibleContent == null || viewModel == null)
        {
            return;
        }

        // Initialize helpers if not already initialized
        if (animationManager == null)
        {
            InitializeHelpers();
        }

        if (animationManager != null)
        {
            animationManager.CachedHeight = null;
        }

        // On initial load, ensure content visibility matches MusicEnabled state without animation
        // Use a small delay to ensure the visual tree is fully initialized
        this.Dispatcher.DispatchAsync(async () =>
        {
            // Wait a bit for the visual tree to be ready
            await Task.Delay(100);

            if (CollapsibleContent != null && viewModel != null && Handler != null && propertyChangeHandler != null)
            {
                if (propertyChangeHandler.IsInitialLoad)
                {
#if DEBUG
                    Log.Debug("[MusicSelectionContainer] OnHandlerChanged: Setting initial visibility - MusicEnabled = {MusicEnabled}", viewModel.MusicEnabled);
#endif
                    visibilityManager?.UpdateCollapsibleContentVisibility(viewModel.MusicEnabled, animate: false);
                }
                else if (viewModel.MusicEnabled && animationManager != null)
                {
                    // Trigger a layout update to measure (for non-initial loads)
                    if (CollapsibleContent.IsVisible && !animationManager.IsAnimating)
                    {
                        CollapsibleContent.HeightRequest = -1;
                    }
                }
            }
        });
    }


    public void Dispose()
    {
        if (!isDisposed)
        {
            propertyChangeHandler?.Dispose();

            // Unsubscribe from view model
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}

