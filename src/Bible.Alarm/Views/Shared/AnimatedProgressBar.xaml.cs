#nullable enable

using Serilog;

namespace Bible.Alarm.Views.Shared;

/// <summary>
/// Animated progress bar that uses native MAUI animations for smooth movement.
/// The animation runs independently of the UI thread workload.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class AnimatedProgressBar : ContentView
{
    private bool isAnimating;
    private CancellationTokenSource? animationCts;

    // Width of the animated segment
    private const double SegmentWidth = 120;

    // Duration for one complete animation cycle (left to right)
    private const uint AnimationDurationMs = 1000;

    public AnimatedProgressBar()
    {
        InitializeComponent();

        // Start animation when the control becomes visible
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IsVisible) || e.PropertyName == nameof(Opacity))
            {
                UpdateAnimationState();
            }
        };

        // Also listen to parent opacity changes
        Loaded += (s, e) => UpdateAnimationState();
        Unloaded += (s, e) => StopAnimation();
    }

    private void UpdateAnimationState()
    {
        // Start animation if visible and has opacity, stop otherwise
        if (IsVisible && Opacity > 0)
        {
            StartAnimation();
        }
        else
        {
            StopAnimation();
        }
    }

    private void StartAnimation()
    {
        if (isAnimating)
        {
            return;
        }

        isAnimating = true;
        animationCts = new CancellationTokenSource();

        // Run continuous animation loop
        _ = AnimateAsync(animationCts.Token);
    }

    private void StopAnimation()
    {
        if (!isAnimating)
        {
            return;
        }

        isAnimating = false;
        animationCts?.Cancel();
        animationCts?.Dispose();
        animationCts = null;

        // Reset to start position
        AnimatedSegment.TranslationX = 0;
    }

    private async Task AnimateAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && isAnimating)
            {
                var containerWidth = await GetTrackWidthAsync();
                if (containerWidth <= 0)
                {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                var maxTranslation = containerWidth - SegmentWidth;
                if (maxTranslation <= 0)
                {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                await AnimatedSegment.TranslateToAsync(maxTranslation, 0, AnimationDurationMs, Easing.SinInOut);

                if (cancellationToken.IsCancellationRequested || !isAnimating)
                {
                    break;
                }

                await AnimatedSegment.TranslateToAsync(0, 0, AnimationDurationMs, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when animation is stopped
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "AnimatedProgressBar: Animation error (non-critical)");
        }
    }

    private Task<double> GetTrackWidthAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (TrackGrid.Width > 0)
            {
                return TrackGrid.Width;
            }
            if (Width > 32)
            {
                return Width - 32;
            }
            return 0;
        });
    }
}

