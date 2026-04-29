#nullable enable

using Serilog;

namespace Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

/// <summary>
/// Manages animation logic for collapsible content in MusicSelectionContainer.
/// </summary>
public class AnimationManager
{
    private static readonly ILogger logger = Log.ForContext<AnimationManager>();
    private readonly View container;
    private readonly View collapsibleContent;
    private bool isAnimating;
    private double? cachedHeight;

    public AnimationManager(View container, View collapsibleContent)
    {
        this.container = container;
        this.collapsibleContent = collapsibleContent;
    }

    public bool IsAnimating => isAnimating;

    public double? CachedHeight
    {
        get => cachedHeight;
        set => cachedHeight = value;
    }

    // Track the last requested state to handle rapid toggles
    private bool? lastRequestedState;

    public async Task AnimateCollapsibleContent(bool isEnabled, Action<bool, bool>? onAnimationComplete = null)
    {
        if (collapsibleContent == null || container.Handler == null)
        {
            return;
        }

        // If already animating, check if the requested state is the same
        if (isAnimating)
        {
            // If same state requested, ignore the duplicate call
            if (lastRequestedState == isEnabled)
            {
                return;
            }
            // Different state requested - abort current animation and start new one
            AbortExistingAnimations();
            isAnimating = false;
        }

        lastRequestedState = isEnabled;
        isAnimating = true;

        try
        {
            AbortExistingAnimations();

            if (isEnabled)
            {
                await AnimateExpand((cancelled) => onAnimationComplete?.Invoke(isEnabled, cancelled));
            }
            else
            {
                await AnimateCollapse((cancelled) => onAnimationComplete?.Invoke(isEnabled, cancelled));
            }
        }
        catch (Exception ex)
        {
            // Animation failures are non-critical - fallback to direct state change
            logger.Debug(ex, "Animation failed, falling back to direct state change. isEnabled: {IsEnabled}", isEnabled);
            SetContentStateDirectly(isEnabled);
            isAnimating = false;
        }
    }

    public void AbortExistingAnimations()
    {
        container.AbortAnimation("ExpandCollapsibleContent");
        container.AbortAnimation("CollapseCollapsibleContent");
    }

    private async Task AnimateExpand(Action<bool>? onAnimationComplete)
    {
        if (collapsibleContent == null) return;

        collapsibleContent.IsVisible = true;
        collapsibleContent.Opacity = 0;

        await EnsureHeightCached();
        if (collapsibleContent == null) return;

        var targetHeight = GetTargetHeight();
        collapsibleContent.HeightRequest = 0;

        var animation = CreateExpandAnimation(targetHeight);
        animation.Commit(container, "ExpandCollapsibleContent", 16, 300, finished: (d, cancelled) =>
        {
            if (collapsibleContent != null && !cancelled)
            {
                collapsibleContent.HeightRequest = -1;
            }
            isAnimating = false;
            onAnimationComplete?.Invoke(!cancelled);
        });
    }

    private async Task AnimateCollapse(Action<bool>? onAnimationComplete)
    {
        if (collapsibleContent == null) return;

        var startHeight = await GetStartHeightForCollapse();
        if (collapsibleContent == null) return;

        collapsibleContent.HeightRequest = startHeight;
        collapsibleContent.Opacity = 1;

        var animation = CreateCollapseAnimation(startHeight);
        animation.Commit(container, "CollapseCollapsibleContent", 16, 300, finished: (d, cancelled) =>
        {
            if (collapsibleContent != null && !cancelled)
            {
                collapsibleContent.IsVisible = false;
                collapsibleContent.HeightRequest = 0;
                collapsibleContent.Opacity = 0;
            }
            isAnimating = false;
            onAnimationComplete?.Invoke(!cancelled);
        });
    }

    private async Task EnsureHeightCached()
    {
        if (collapsibleContent == null) return;

        if (!cachedHeight.HasValue || cachedHeight.Value <= 0)
        {
            collapsibleContent.HeightRequest = -1;
            collapsibleContent.Opacity = 1;
            await Task.Delay(100);

            if (collapsibleContent != null)
            {
                cachedHeight = collapsibleContent.Height > 0 ? collapsibleContent.Height : 200;
                collapsibleContent.Opacity = 0;
            }
        }
    }

    private double GetTargetHeight()
    {
        return cachedHeight.HasValue && cachedHeight.Value > 0 ? cachedHeight.Value : 200;
    }

    private async Task<double> GetStartHeightForCollapse()
    {
        var currentHeight = collapsibleContent.Height;

        if (currentHeight <= 0)
        {
            if (!cachedHeight.HasValue || cachedHeight.Value <= 0)
            {
                collapsibleContent.HeightRequest = -1;
                await Task.Delay(50);
                currentHeight = collapsibleContent.Height;
            }

            var useCachedFallback = currentHeight <= 0;
            if (useCachedFallback)
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

    private Animation CreateExpandAnimation(double targetHeight)
    {
        var heightAnimation = new Animation(
            value => { if (collapsibleContent != null) collapsibleContent.HeightRequest = value; },
            0, targetHeight, easing: Easing.CubicOut);

        var opacityAnimation = new Animation(
            value => { if (collapsibleContent != null) collapsibleContent.Opacity = value; },
            0, 1, easing: Easing.CubicOut);

        var parentAnimation = new Animation();
        parentAnimation.Add(0, 1, heightAnimation);
        parentAnimation.Add(0, 1, opacityAnimation);
        return parentAnimation;
    }

    private Animation CreateCollapseAnimation(double startHeight)
    {
        var heightAnimation = new Animation(
            value => { if (collapsibleContent != null) collapsibleContent.HeightRequest = value; },
            startHeight, 0, easing: Easing.CubicIn);

        var opacityAnimation = new Animation(
            value => { if (collapsibleContent != null) collapsibleContent.Opacity = value; },
            1, 0, easing: Easing.CubicIn);

        var parentAnimation = new Animation();
        parentAnimation.Add(0, 1, heightAnimation);
        parentAnimation.Add(0, 1, opacityAnimation);
        return parentAnimation;
    }

    public void SetContentStateDirectly(bool isEnabled)
    {
        if (collapsibleContent != null)
        {
            collapsibleContent.IsVisible = isEnabled;
            collapsibleContent.Opacity = isEnabled ? 1 : 0;
            collapsibleContent.HeightRequest = isEnabled ? -1 : 0;
        }
    }
}

