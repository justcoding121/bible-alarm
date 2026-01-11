#nullable enable

using System.ComponentModel;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;
using Serilog;

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
#if DEBUG
            Log.Debug("[MusicSelectionContainer] Constructor - Subscribing to PropertyChanged. ViewModel: {ViewModelType}, Handler: {HasHandler}",
                viewModel.GetType().Name, propertyChangeHandler != null);
#endif
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (propertyChangeHandler != null)
            {
                propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
                propertyChangeHandler.ShouldScrollOnExpand = false; // Don't scroll on initial load
                propertyChangeHandler.IsInitialLoad = true; // Mark as initial load
            }
#if DEBUG
            Log.Debug("[MusicSelectionContainer] Constructor - Subscribed. Initial MusicEnabled: {MusicEnabled}, LastState: {LastState}",
                viewModel?.MusicEnabled ?? false, propertyChangeHandler?.LastMusicEnabledState ?? false);
#endif

            // Immediately process any pending property changes that might have occurred before subscription
            // This ensures we don't miss property changes that were raised before the view subscribed
            Dispatcher.Dispatch(() =>
            {
                if (viewModel != null && propertyChangeHandler != null)
                {
                    // Trigger property change handler to sync with current state
                    var currentMusicEnabled = viewModel.MusicEnabled;
                    if (currentMusicEnabled != propertyChangeHandler.LastMusicEnabledState)
                    {
#if DEBUG
                        Log.Debug("[MusicSelectionContainer] Constructor - Syncing with current MusicEnabled state: {MusicEnabled}", currentMusicEnabled);
#endif
                        propertyChangeHandler.OnViewModelPropertyChanged(viewModel, new System.ComponentModel.PropertyChangedEventArgs(nameof(MusicSelectionContainerViewModel.MusicEnabled)), viewModel);
                    }
                }
            });

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

#if DEBUG
        Log.Debug("[MusicSelectionContainer] OnBindingContextChanged called. Old ViewModel: {OldViewModel}, New BindingContext: {NewBindingContext}",
            viewModel?.GetType().Name ?? "null",
            BindingContext?.GetType().Name ?? "null");
#endif

        // Unsubscribe from old view model
        if (viewModel != null)
        {
#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnBindingContextChanged - Unsubscribing from old ViewModel");
#endif
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new view model
        var newViewModel = BindingContext as MusicSelectionContainerViewModel;

        // Only reset isInitialLoad if this is actually a different ViewModel instance
        var isNewViewModel = newViewModel != null && newViewModel != viewModel;

        viewModel = newViewModel;

        // Ensure helpers are initialized before subscribing
        // This is important because InitializeHelpers requires CollapsibleContent to be non-null
        // which might not be the case when OnBindingContextChanged is called early
        if (propertyChangeHandler == null && CollapsibleContent != null)
        {
#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnBindingContextChanged - Initializing helpers (CollapsibleContent is ready)");
#endif
            InitializeHelpers();
        }

        if (viewModel != null && propertyChangeHandler != null)
        {
#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnBindingContextChanged - Subscribing to PropertyChanged. ViewModel: {ViewModelType}, Handler: {HasHandler}, IsNewViewModel: {IsNew}",
                viewModel.GetType().Name, propertyChangeHandler != null, isNewViewModel);
#endif
            // Unsubscribe first to prevent duplicate subscriptions
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (viewModel != null && propertyChangeHandler != null)
            {
                propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
            }
#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnBindingContextChanged - Subscribed. Initial MusicEnabled: {MusicEnabled}, LastState: {LastState}",
                viewModel?.MusicEnabled ?? false, propertyChangeHandler?.LastMusicEnabledState ?? false);
#endif

            // Immediately process any pending property changes that might have occurred before subscription
            // This ensures we don't miss property changes that were raised before the view subscribed
            Dispatcher.Dispatch(() =>
            {
                if (viewModel != null && propertyChangeHandler != null)
                {
                    // Trigger property change handler to sync with current state
                    var currentMusicEnabled = viewModel.MusicEnabled;
                    if (currentMusicEnabled != propertyChangeHandler.LastMusicEnabledState)
                    {
#if DEBUG
                        Log.Debug("[MusicSelectionContainer] OnBindingContextChanged - Syncing with current MusicEnabled state: {MusicEnabled}", currentMusicEnabled);
#endif
                        propertyChangeHandler.OnViewModelPropertyChanged(viewModel, new System.ComponentModel.PropertyChangedEventArgs(nameof(MusicSelectionContainerViewModel.MusicEnabled)), viewModel);
                    }
                }
            });

            // Only reset isInitialLoad if this is a new ViewModel instance
            // If it's the same ViewModel being reassigned, keep the current isInitialLoad state
            if (isNewViewModel && propertyChangeHandler != null)
            {
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
        else
        {
#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnBindingContextChanged - Cannot subscribe. ViewModel: {HasViewModel}, Handler: {HasHandler}",
                viewModel != null, propertyChangeHandler != null);
#endif
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
#if DEBUG
        Log.Debug("[MusicSelectionContainer] OnViewModelPropertyChanged received: Property={PropertyName}, Sender type: {SenderType}, Handler: {HasHandler}, ViewModel: {HasViewModel}",
            e.PropertyName ?? "null",
            sender?.GetType().Name ?? "null",
            propertyChangeHandler != null,
            viewModel != null);
#endif
        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.MusicEnabled))
        {
#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnViewModelPropertyChanged: MusicEnabled property changed. Calling handler.");
#endif
        }

        propertyChangeHandler?.OnViewModelPropertyChanged(sender, e, viewModel);
    }


    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler == null || CollapsibleContent == null)
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

        // Ensure subscription happens when handler is ready
        // This is important because OnBindingContextChanged might be called before CollapsibleContent is ready
        if (viewModel == null)
        {
            viewModel = BindingContext as MusicSelectionContainerViewModel;
        }

        if (viewModel != null && propertyChangeHandler != null)
        {
            // Check if we're already subscribed (avoid duplicate subscriptions)
            // We can't easily check if an event handler is subscribed, so we'll just subscribe
            // Event handlers can be safely subscribed multiple times, but we want to avoid it
            // For now, we'll unsubscribe first to ensure clean subscription
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

#if DEBUG
            Log.Debug("[MusicSelectionContainer] OnHandlerChanged - Ensured subscription. MusicEnabled: {MusicEnabled}, LastState: {LastState}",
                viewModel.MusicEnabled, propertyChangeHandler.LastMusicEnabledState);
#endif

            // Update last state to match current state
            propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
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

