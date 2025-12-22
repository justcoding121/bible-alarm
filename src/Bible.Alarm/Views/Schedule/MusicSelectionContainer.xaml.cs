using Bible.Alarm.ViewModels.Schedule;
using System.ComponentModel;
using System.Threading;

namespace Bible.Alarm.Views.Schedule;

public partial class MusicSelectionContainer : ContentView
{
    private bool isAnimating;
    private double? cachedHeight;
    private MusicSelectionContainerViewModel? viewModel;
    private bool lastMusicEnabledState;
    private CancellationTokenSource? debounceTokenSource;

    public MusicSelectionContainer()
    {
        InitializeComponent();
    }

    public MusicSelectionContainer(MusicSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
        this.viewModel = viewModel;
        
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            lastMusicEnabledState = viewModel.MusicEnabled;
            // Set initial state
            UpdateCollapsibleContentVisibility(viewModel.MusicEnabled, animate: false);
        }
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
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            lastMusicEnabledState = viewModel.MusicEnabled;
            // Set initial state
            UpdateCollapsibleContentVisibility(viewModel.MusicEnabled, animate: false);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.MusicEnabled) && 
            sender is MusicSelectionContainerViewModel vm)
        {
            var newState = vm.MusicEnabled;
            
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] PropertyChanged: MusicEnabled = {newState}, LastState = {lastMusicEnabledState}");
            
            // Debounce rapid changes
            if (newState == lastMusicEnabledState)
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] State unchanged, ignoring");
                return; // Ignore if state hasn't actually changed
            }
            
            lastMusicEnabledState = newState;
            
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Triggering animation for MusicEnabled = {newState}");
            
            // Cancel any pending debounce
            debounceTokenSource?.Cancel();
            debounceTokenSource = new CancellationTokenSource();
            var token = debounceTokenSource.Token;
            
            // Small delay to debounce rapid changes, but trigger animation immediately on UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!token.IsCancellationRequested && Handler != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Calling UpdateCollapsibleContentVisibility with animate=true, isEnabled={newState}");
                    UpdateCollapsibleContentVisibility(newState, animate: true);
                }
            });
        }
    }

    private void UpdateCollapsibleContentVisibility(bool isEnabled, bool animate)
    {
        System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] UpdateCollapsibleContentVisibility: isEnabled={isEnabled}, animate={animate}, CollapsibleContent={CollapsibleContent != null}, isAnimating={isAnimating}");
        
        if (CollapsibleContent == null)
        {
            System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] CollapsibleContent is null, returning");
            return;
        }
        
        if (isAnimating)
        {
            System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Already animating, returning");
            return;
        }

        if (animate)
        {
            System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Starting animation");
            AnimateCollapsibleContent(isEnabled);
        }
        else
        {
            // Set initial state without animation
            System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Setting initial state without animation");
            CollapsibleContent.IsVisible = isEnabled;
            CollapsibleContent.Opacity = isEnabled ? 1 : 0;
            if (!isEnabled)
            {
                CollapsibleContent.HeightRequest = 0;
            }
            else
            {
                CollapsibleContent.HeightRequest = -1; // Auto
            }
        }
    }

    private async void AnimateCollapsibleContent(bool isEnabled)
    {
        System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] AnimateCollapsibleContent: isEnabled={isEnabled}, CollapsibleContent={CollapsibleContent != null}, isAnimating={isAnimating}, Handler={Handler != null}");
        
        if (CollapsibleContent == null || isAnimating || Handler == null)
        {
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] AnimateCollapsibleContent: Early return - CollapsibleContent={CollapsibleContent != null}, isAnimating={isAnimating}, Handler={Handler != null}");
            return;
        }

        isAnimating = true;

        try
        {
            // Cancel any existing animations
            this.AbortAnimation("ExpandCollapsibleContent");
            this.AbortAnimation("CollapseCollapsibleContent");

            if (isEnabled)
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Starting EXPAND animation");
                
                // Expand animation
                if (CollapsibleContent == null) return;
                
                CollapsibleContent.IsVisible = true;
                CollapsibleContent.Opacity = 0;
                
                // Measure the content to get its natural height
                if (!cachedHeight.HasValue || cachedHeight.Value <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Measuring content height...");
                    CollapsibleContent.HeightRequest = -1; // Auto size to measure
                    CollapsibleContent.Opacity = 1;
                    await Task.Delay(100); // Allow layout to measure - increased delay
                    
                    if (CollapsibleContent == null) return;
                    
                    cachedHeight = CollapsibleContent.Height > 0 ? CollapsibleContent.Height : 200;
                    System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Measured height: {cachedHeight}");
                    CollapsibleContent.Opacity = 0;
                }

                if (CollapsibleContent == null) return;

                var targetHeight = cachedHeight.Value;
                if (targetHeight <= 0) targetHeight = 200; // Fallback
                
                System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Expanding to height: {targetHeight}");
                CollapsibleContent.HeightRequest = 0;

                // Animate height and opacity
                var heightAnimation = new Animation(
                    value =>
                    {
                        if (CollapsibleContent != null)
                        {
                            CollapsibleContent.HeightRequest = value;
                        }
                    },
                    0,
                    targetHeight,
                    easing: Easing.CubicOut);

                var opacityAnimation = new Animation(
                    value =>
                    {
                        if (CollapsibleContent != null)
                        {
                            CollapsibleContent.Opacity = value;
                        }
                    },
                    0,
                    1,
                    easing: Easing.CubicOut);

                var parentAnimation = new Animation();
                parentAnimation.Add(0, 1, heightAnimation);
                parentAnimation.Add(0, 1, opacityAnimation);

                parentAnimation.Commit(this, "ExpandCollapsibleContent", 16, 300, finished: (d, cancelled) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Expand animation finished: cancelled={cancelled}");
                    // Reset to auto after animation completes
                    if (CollapsibleContent != null && !cancelled)
                    {
                        CollapsibleContent.HeightRequest = -1;
                    }
                    isAnimating = false;
                    
                    // Scroll to show the expanded content
                    if (!cancelled)
                    {
                        ScrollToExpandedContent();
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Starting COLLAPSE animation");
                
                // Collapse animation
                if (CollapsibleContent == null) return;
                
                // Get current height before collapsing - ensure we have a valid height
                var currentHeight = CollapsibleContent.Height;
                System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Current height before collapse: {currentHeight}");
                
                if (currentHeight <= 0)
                {
                    // Try to measure if not already measured
                    if (!cachedHeight.HasValue || cachedHeight.Value <= 0)
                    {
                        CollapsibleContent.HeightRequest = -1;
                        await Task.Delay(50);
                        if (CollapsibleContent == null) return;
                        currentHeight = CollapsibleContent.Height;
                        System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Measured height during collapse: {currentHeight}");
                    }
                    
                    if (currentHeight <= 0)
                    {
                        currentHeight = cachedHeight ?? 200;
                        System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Using cached/fallback height: {currentHeight}");
                    }
                    
                    cachedHeight = currentHeight;
                }
                else
                {
                    // Cache the height for future use
                    cachedHeight = currentHeight;
                }

                var startHeight = currentHeight;
                if (startHeight <= 0) startHeight = 200; // Fallback
                
                System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Collapsing from height: {startHeight}");
                CollapsibleContent.HeightRequest = startHeight;
                CollapsibleContent.Opacity = 1;

                // Animate height and opacity
                var heightAnimation = new Animation(
                    value =>
                    {
                        if (CollapsibleContent != null)
                        {
                            CollapsibleContent.HeightRequest = value;
                        }
                    },
                    startHeight,
                    0,
                    easing: Easing.CubicIn);

                var opacityAnimation = new Animation(
                    value =>
                    {
                        if (CollapsibleContent != null)
                        {
                            CollapsibleContent.Opacity = value;
                        }
                    },
                    1,
                    0,
                    easing: Easing.CubicIn);

                var parentAnimation = new Animation();
                parentAnimation.Add(0, 1, heightAnimation);
                parentAnimation.Add(0, 1, opacityAnimation);

                parentAnimation.Commit(this, "CollapseCollapsibleContent", 16, 300, finished: (d, cancelled) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Collapse animation finished: cancelled={cancelled}");
                    // Hide after animation completes
                    if (CollapsibleContent != null && !cancelled)
                    {
                        CollapsibleContent.IsVisible = false;
                        CollapsibleContent.HeightRequest = 0;
                        CollapsibleContent.Opacity = 0;
                    }
                    isAnimating = false;
                });
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - just set the state directly
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Animation error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Stack trace: {ex.StackTrace}");
            
            if (CollapsibleContent != null)
            {
                CollapsibleContent.IsVisible = isEnabled;
                CollapsibleContent.Opacity = isEnabled ? 1 : 0;
                CollapsibleContent.HeightRequest = isEnabled ? -1 : 0;
            }
            isAnimating = false;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Re-measure when handler is ready
        if (Handler != null && CollapsibleContent != null && viewModel != null)
        {
            cachedHeight = null;
            if (viewModel.MusicEnabled)
            {
                // Trigger a layout update to measure
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                {
                    if (CollapsibleContent != null && CollapsibleContent.IsVisible && !isAnimating)
                    {
                        CollapsibleContent.HeightRequest = -1;
                    }
                });
            }
        }
    }

    private void ScrollToExpandedContent()
    {
        try
        {
            // Find the parent ScrollView by traversing up the visual tree
            var parent = this.Parent;
            ScrollView? scrollView = null;
            
            while (parent != null)
            {
                if (parent is ScrollView sv)
                {
                    scrollView = sv;
                    break;
                }
                parent = parent.Parent;
            }

            if (scrollView != null)
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Found ScrollView, scrolling to bottom");
                
                // Scroll to bottom with animation
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(150); // Small delay to ensure layout is complete after animation
                    
                    // Scroll to the bottom using coordinates
                    // Wait a bit more for content height to be measured
                    await Task.Delay(50);
                    
                    // Try to get the content height
                    var contentHeight = scrollView.Content.Height;
                    if (contentHeight > 0)
                    {
                        await scrollView.ScrollToAsync(0, contentHeight, true);
                        System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Scrolled to bottom (height: {contentHeight})");
                    }
                    else
                    {
                        // If height not available, try scrolling to the last child element
                        if (scrollView.Content is Layout layout && layout.Children.Count > 0)
                        {
                            var lastChild = layout.Children[layout.Children.Count - 1];
                            if (lastChild is Element element)
                            {
                                await scrollView.ScrollToAsync(element, ScrollToPosition.End, true);
                                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Scrolled to bottom (last child element)");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Last child is not an Element, cannot scroll");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Content height not available and no children found");
                        }
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] ScrollView not found");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Error scrolling: {ex.Message}");
        }
    }
}

