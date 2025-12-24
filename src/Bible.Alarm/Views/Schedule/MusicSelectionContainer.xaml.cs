#nullable enable

using Bible.Alarm.ViewModels.Schedule;
using System.ComponentModel;
using System.Threading;

namespace Bible.Alarm.Views.Schedule;

public partial class MusicSelectionContainer : ContentView, IDisposable
{
    private bool isAnimating;
    private double? cachedHeight;
    private MusicSelectionContainerViewModel? viewModel;
    private bool lastMusicEnabledState;
    private CancellationTokenSource? debounceTokenSource;
    private bool shouldScrollOnExpand; // Track if we should scroll when expanding
    private bool isInitialLoad = true; // Track if this is the initial load
    private bool isDisposed;

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
            shouldScrollOnExpand = false; // Don't scroll on initial load
            isInitialLoad = true; // Mark as initial load
            // Don't set initial state here - wait for OnHandlerChanged when CollapsibleContent is ready
            // Mark initial load as complete after a short delay to allow any property changes to settle
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                isInitialLoad = false;
            });
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
            shouldScrollOnExpand = false; // Don't scroll on initial load
            isInitialLoad = true; // Mark as initial load
            // Don't set initial state here - wait for OnHandlerChanged when CollapsibleContent is ready
            // Mark initial load as complete after a short delay to allow any property changes to settle
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                isInitialLoad = false;
            });
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MusicSelectionContainerViewModel vm)
        {
            return;
        }
        
        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.MusicEnabled))
        {
            var newState = vm.MusicEnabled;
            
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] PropertyChanged: MusicEnabled = {newState}, LastState = {lastMusicEnabledState}, isInitialLoad = {isInitialLoad}");
            
            // Ignore property changes during initial load
            if (isInitialLoad)
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] Ignoring property change during initial load");
                lastMusicEnabledState = newState; // Update last state but don't animate
                return;
            }
            
            // Debounce rapid changes
            if (newState == lastMusicEnabledState)
            {
                System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] State unchanged, ignoring");
                return; // Ignore if state hasn't actually changed
            }
            
            // Only scroll if user is toggling from false to true (user-initiated expand)
            shouldScrollOnExpand = !lastMusicEnabledState && newState;
            
            lastMusicEnabledState = newState;
            
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Triggering animation for MusicEnabled = {newState}, shouldScrollOnExpand = {shouldScrollOnExpand}");
            
            // Cancel and dispose any pending debounce
            debounceTokenSource?.Cancel();
            debounceTokenSource?.Dispose();
            debounceTokenSource = new CancellationTokenSource();
            var token = debounceTokenSource.Token;
            
            // Small delay to debounce rapid changes, but trigger update immediately on UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!token.IsCancellationRequested && Handler != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Calling UpdateCollapsibleContentVisibility with animate=false, isEnabled={newState}");
                    UpdateCollapsibleContentVisibility(newState, animate: false);
                }
            });
        }
        else if (e.PropertyName == nameof(MusicSelectionContainerViewModel.ShouldScrollToBottom) && vm.ShouldScrollToBottom)
        {
            // Scroll to bottom when ViewModel signals it
            System.Diagnostics.Debug.WriteLine("[MusicSelectionContainer] ShouldScrollToBottom property changed, scrolling to bottom");
            
            // Small delay to ensure layout is complete
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(200); // Delay to allow UI to update
                ScrollToExpandedContent();
                
                // Reset the flag after scrolling
                if (viewModel != null)
                {
                    viewModel.ShouldScrollToBottom = false;
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
            _ = AnimateCollapsibleContent(isEnabled);
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

    private async Task AnimateCollapsibleContent(bool isEnabled)
    {
        if (CollapsibleContent == null || isAnimating || Handler == null)
        {
            return;
        }

        isAnimating = true;

        try
        {
            AbortExistingAnimations();

            if (isEnabled)
            {
                await AnimateExpand();
            }
            else
            {
                await AnimateCollapse();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Animation error: {ex.Message}");
            SetContentStateDirectly(isEnabled);
            isAnimating = false;
        }
    }

    private void AbortExistingAnimations()
    {
        this.AbortAnimation("ExpandCollapsibleContent");
        this.AbortAnimation("CollapseCollapsibleContent");
    }

    private async Task AnimateExpand()
    {
        if (CollapsibleContent == null) return;

        CollapsibleContent.IsVisible = true;
        CollapsibleContent.Opacity = 0;

        await EnsureHeightCached();
        if (CollapsibleContent == null) return;

        var targetHeight = GetTargetHeight();
        CollapsibleContent.HeightRequest = 0;

        var animation = CreateExpandAnimation(targetHeight);
        animation.Commit(this, "ExpandCollapsibleContent", 16, 300, finished: (d, cancelled) =>
        {
            if (CollapsibleContent != null && !cancelled)
            {
                CollapsibleContent.HeightRequest = -1;
            }
            isAnimating = false;

            if (!cancelled && shouldScrollOnExpand)
            {
                ScrollToExpandedContent();
                shouldScrollOnExpand = false;
            }
        });
    }

    private async Task EnsureHeightCached()
    {
        if (CollapsibleContent == null) return;

        if (!cachedHeight.HasValue || cachedHeight.Value <= 0)
        {
            CollapsibleContent.HeightRequest = -1;
            CollapsibleContent.Opacity = 1;
            await Task.Delay(100);

            if (CollapsibleContent != null)
            {
                cachedHeight = CollapsibleContent.Height > 0 ? CollapsibleContent.Height : 200;
                CollapsibleContent.Opacity = 0;
            }
        }
    }

    private double GetTargetHeight()
    {
        return cachedHeight.HasValue && cachedHeight.Value > 0 ? cachedHeight.Value : 200;
    }

    private Animation CreateExpandAnimation(double targetHeight)
    {
        var heightAnimation = new Animation(
            value => { if (CollapsibleContent != null) CollapsibleContent.HeightRequest = value; },
            0, targetHeight, easing: Easing.CubicOut);

        var opacityAnimation = new Animation(
            value => { if (CollapsibleContent != null) CollapsibleContent.Opacity = value; },
            0, 1, easing: Easing.CubicOut);

        var parentAnimation = new Animation();
        parentAnimation.Add(0, 1, heightAnimation);
        parentAnimation.Add(0, 1, opacityAnimation);
        return parentAnimation;
    }

    private async Task AnimateCollapse()
    {
        if (CollapsibleContent == null) return;

        var startHeight = await GetStartHeightForCollapse();
        if (CollapsibleContent == null) return;

        CollapsibleContent.HeightRequest = startHeight;
        CollapsibleContent.Opacity = 1;

        var animation = CreateCollapseAnimation(startHeight);
        animation.Commit(this, "CollapseCollapsibleContent", 16, 300, finished: (d, cancelled) =>
        {
            if (CollapsibleContent != null && !cancelled)
            {
                CollapsibleContent.IsVisible = false;
                CollapsibleContent.HeightRequest = 0;
                CollapsibleContent.Opacity = 0;
            }
            isAnimating = false;
        });
    }

    private async Task<double> GetStartHeightForCollapse()
    {
        if (CollapsibleContent == null) return 200;

        var currentHeight = CollapsibleContent.Height;

        if (currentHeight <= 0)
        {
            if (!cachedHeight.HasValue || cachedHeight.Value <= 0)
            {
                CollapsibleContent.HeightRequest = -1;
                await Task.Delay(50);
                if (CollapsibleContent != null)
                {
                    currentHeight = CollapsibleContent.Height;
                }
            }

            if (currentHeight <= 0)
            {
                currentHeight = cachedHeight ?? 200;
            }

            cachedHeight = currentHeight;
        }
        else
        {
            cachedHeight = currentHeight;
        }

        return currentHeight > 0 ? currentHeight : 200;
    }

    private Animation CreateCollapseAnimation(double startHeight)
    {
        var heightAnimation = new Animation(
            value => { if (CollapsibleContent != null) CollapsibleContent.HeightRequest = value; },
            startHeight, 0, easing: Easing.CubicIn);

        var opacityAnimation = new Animation(
            value => { if (CollapsibleContent != null) CollapsibleContent.Opacity = value; },
            1, 0, easing: Easing.CubicIn);

        var parentAnimation = new Animation();
        parentAnimation.Add(0, 1, heightAnimation);
        parentAnimation.Add(0, 1, opacityAnimation);
        return parentAnimation;
    }

    private void SetContentStateDirectly(bool isEnabled)
    {
        if (CollapsibleContent != null)
        {
            CollapsibleContent.IsVisible = isEnabled;
            CollapsibleContent.Opacity = isEnabled ? 1 : 0;
            CollapsibleContent.HeightRequest = isEnabled ? -1 : 0;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Re-measure when handler is ready
        if (Handler != null && CollapsibleContent != null && viewModel != null)
        {
            cachedHeight = null;
            
            // On initial load, ensure content visibility matches MusicEnabled state without animation
            // Use a small delay to ensure the visual tree is fully initialized
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Wait a bit for the visual tree to be ready
                await Task.Delay(100);
                
                if (CollapsibleContent != null && viewModel != null && Handler != null)
                {
                    if (isInitialLoad)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] OnHandlerChanged: Setting initial visibility - MusicEnabled = {viewModel.MusicEnabled}");
                        UpdateCollapsibleContentVisibility(viewModel.MusicEnabled, animate: false);
                    }
                    else if (viewModel.MusicEnabled)
                    {
                        // Trigger a layout update to measure (for non-initial loads)
                        if (CollapsibleContent.IsVisible && !isAnimating)
                        {
                            CollapsibleContent.HeightRequest = -1;
                        }
                    }
                }
            });
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

    public void Dispose()
    {
        if (!isDisposed)
        {
            // Cancel and dispose cancellation token source
            try
            {
                debounceTokenSource?.Cancel();
                debounceTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore errors during cancellation/disposal
                System.Diagnostics.Debug.WriteLine($"[MusicSelectionContainer] Error during debounceTokenSource disposal: {ex.Message}");
            }

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

