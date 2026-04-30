#nullable enable

using System.ComponentModel;
using Bible.Alarm.Shared.Constants;
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
        WireUpSwitchAndButtonHandlers();
    }

    public MusicSelectionContainer(MusicSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Set initial visibility based on IsMusicSelectionVisible
        IsVisible = viewModel.IsMusicSelectionVisible;

        InitializeHelpers();

        if (propertyChangeHandler != null)
        {
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ConstructorSubscribingPropertyChanged,
                viewModel.GetType().Name, true);
#endif
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
            propertyChangeHandler.ShouldScrollOnExpand = false; // Don't scroll on initial load
            propertyChangeHandler.IsInitialLoad = true; // Mark as initial load
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ConstructorSubscribedInitialMusicEnabled,
                viewModel?.MusicEnabled ?? false, propertyChangeHandler?.LastMusicEnabledState ?? false);
#endif

            // Immediately process any pending property changes that might have occurred before subscription
            // This ensures we don't miss property changes that were raised before the view subscribed
            Dispatcher.Dispatch(() =>
            {
                if (propertyChangeHandler != null)
                {
                    // Trigger property change handler to sync with current state
                    var currentMusicEnabled = viewModel.MusicEnabled;
                    if (currentMusicEnabled != propertyChangeHandler.LastMusicEnabledState)
                    {
#if DEBUG
                        Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ConstructorSyncingMusicEnabledState, currentMusicEnabled);
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

    private void WireUpSwitchAndButtonHandlers()
    {
        // Start traversal from Content property, not the ContentView itself
        if (Content is View content)
        {
            FindAndWireSwitchesAndButtons(content);
        }
    }

    private void FindAndWireSwitchesAndButtons(View view)
    {
        if (view == null)
        {
            return;
        }

        if (view is Shared.PlatformSwitch platformSwitch)
        {
            platformSwitch.PropertyChanged -= OnSwitchPropertyChanged; // Remove first to avoid duplicates
            platformSwitch.PropertyChanged += OnSwitchPropertyChanged;
            return;
        }

        if (view is Button button)
        {
            button.Clicked -= OnButtonClicked; // Remove first to avoid duplicates
            button.Clicked += OnButtonClicked;
            return;
        }

        // Handle different container types
        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View childView)
                {
                    FindAndWireSwitchesAndButtons(childView);
                }
            }
        }
        else if (view is ContentView contentView && contentView.Content is View content)
        {
            FindAndWireSwitchesAndButtons(content);
        }
        else if (view is Border border && border.Content is View borderContent)
        {
            FindAndWireSwitchesAndButtons(borderContent);
        }
        else if (view is Syncfusion.Maui.Core.SfEffectsView sfEffectsView && sfEffectsView.Content is View sfContent)
        {
            FindAndWireSwitchesAndButtons(sfContent);
        }
    }

    private void OnSwitchPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Shared.PlatformSwitch.IsToggled))
        {
            UnfocusScheduleNameEntry();
        }
    }

    private void OnButtonClicked(object? sender, EventArgs e)
    {
        UnfocusScheduleNameEntry();
    }

    private void UnfocusScheduleNameEntry()
    {
        // Find parent ScheduleContent and call its unfocus method
        var parent = Parent;
        while (parent != null)
        {
            if (parent is ScheduleContent scheduleContent)
            {
                scheduleContent.UnfocusScheduleNameEntry();
                break;
            }
            parent = parent.Parent;
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
                if (propertyChangeHandler?.ShouldScrollOnExpand is true)
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
        Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedCalled,
            viewModel?.GetType().Name ?? "null",
            BindingContext?.GetType().Name ?? "null");
#endif

        // Unsubscribe from old view model
        if (viewModel != null)
        {
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedUnsubscribingOldViewModel);
#endif
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new view model
        var newViewModel = BindingContext as MusicSelectionContainerViewModel;

        // Only reset isInitialLoad if this is actually a different ViewModel instance
        var isNewViewModel = newViewModel != null && newViewModel != viewModel;

        viewModel = newViewModel;
        
        // Set initial visibility based on IsMusicSelectionVisible
        if (viewModel != null)
        {
            IsVisible = viewModel.IsMusicSelectionVisible;
        }

        // Ensure helpers are initialized before subscribing
        // This is important because InitializeHelpers requires CollapsibleContent to be non-null
        // which might not be the case when OnBindingContextChanged is called early
        if (propertyChangeHandler == null && CollapsibleContent != null)
        {
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedInitializingHelpers);
#endif
            InitializeHelpers();
        }

        if (viewModel != null && propertyChangeHandler != null)
        {
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedSubscribingPropertyChanged,
                viewModel.GetType().Name, true, isNewViewModel);
#endif
            // Unsubscribe first to prevent duplicate subscriptions
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            propertyChangeHandler.LastMusicEnabledState = viewModel.MusicEnabled;
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedSubscribedInitialMusicEnabled,
                viewModel?.MusicEnabled ?? false, propertyChangeHandler?.LastMusicEnabledState ?? false);
#endif

            // Immediately process any pending property changes that might have occurred before subscription
            // This ensures we don't miss property changes that were raised before the view subscribed
            Dispatcher.Dispatch(() =>
            {
                if (propertyChangeHandler != null)
                {
                    // Trigger property change handler to sync with current state
                    var currentMusicEnabled = viewModel.MusicEnabled;
                    if (currentMusicEnabled != propertyChangeHandler.LastMusicEnabledState)
                    {
#if DEBUG
                        Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedSyncingMusicEnabledState, currentMusicEnabled);
#endif
                        propertyChangeHandler.OnViewModelPropertyChanged(viewModel, new System.ComponentModel.PropertyChangedEventArgs(nameof(MusicSelectionContainerViewModel.MusicEnabled)), viewModel);
                    }
                }
            });

            // Only reset isInitialLoad if this is a new ViewModel instance
            // If it's the same ViewModel being reassigned, keep the current isInitialLoad state
            if (isNewViewModel)
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
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnBindingContextChangedCannotSubscribe,
                viewModel != null, propertyChangeHandler != null);
#endif
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
#if DEBUG
        Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnViewModelPropertyChangedReceived,
            e.PropertyName ?? "null",
            sender?.GetType().Name ?? "null",
            propertyChangeHandler != null,
            viewModel != null);
#endif
        
        // Handle IsMusicSelectionVisible property change - explicitly update IsVisible binding
        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.IsMusicSelectionVisible))
        {
            if (viewModel == null)
            {
                return;
            }

            var newVisible = viewModel.IsMusicSelectionVisible;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isDisposed || Handler == null)
                {
                    return;
                }

                try
                {
                    IsVisible = newVisible;
                }
                catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x8001010E))
                {
                    Log.Debug(ex, AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.SkippingIsVisibleUpdateViewDisconnected);
                }
            });
            return; // Don't pass to propertyChangeHandler as this is handled here
        }
        
        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.MusicEnabled))
        {
#if DEBUG
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnViewModelPropertyChangedMusicEnabledCallingHandler);
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

        // Wire up handlers after visual tree is ready
        Dispatcher.Dispatch(() =>
        {
            WireUpSwitchAndButtonHandlers();
        });

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
            Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnHandlerChangedEnsuredSubscription,
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

            var ready = CollapsibleContent != null && viewModel != null && Handler != null && propertyChangeHandler != null;
            if (!ready)
            {
                return;
            }

            if (propertyChangeHandler!.IsInitialLoad)
            {
#if DEBUG
                Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.OnHandlerChangedSettingInitialVisibility, viewModel!.MusicEnabled);
#endif
                visibilityManager?.UpdateCollapsibleContentVisibility(viewModel!.MusicEnabled, animate: false);
            }
            else if (viewModel!.MusicEnabled && animationManager != null
                     && CollapsibleContent!.IsVisible && !animationManager.IsAnimating)
            {
                // Trigger a layout update to measure (for non-initial loads)
                CollapsibleContent.HeightRequest = -1;
            }
        });
    }


    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            propertyChangeHandler?.Dispose();

            // Unsubscribe from view model
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
        }

        isDisposed = true;
    }
}

