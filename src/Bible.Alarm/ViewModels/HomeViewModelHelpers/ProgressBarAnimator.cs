#nullable enable
namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles progress bar animation logic.
/// Separated from HomeViewModel for better modularity.
/// </summary>
public class ProgressBarAnimator : IDisposable
{
    private System.Timers.Timer? progressAnimationTimer;
    private const double RangeWidth = 0.3; // 30% of the bar width

    public double AnimatedProgressStart { get; private set; } = 0.0;
    public double AnimatedProgressEnd { get; private set; } = 0.3;
    public double AnimatedProgress => AnimatedProgressEnd;
    public double AnimatedProgressRangeWidth => RangeWidth;

    public event Action? ProgressChanged;

    public void Start()
    {
        if (progressAnimationTimer != null)
        {
            return; // Already animating
        }

        // Reset to start position
        AnimatedProgressStart = 0.0;
        AnimatedProgressEnd = RangeWidth;

        // Animate a range segment moving from left to right
        progressAnimationTimer = new System.Timers.Timer(20); // Update every 20ms
        progressAnimationTimer.Elapsed += (sender, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Move the range forward
                AnimatedProgressStart += 0.04; // Move 4% per frame
                AnimatedProgressEnd = AnimatedProgressStart + RangeWidth;

                // When range reaches the end, reset to start
                if (AnimatedProgressStart >= 1.0)
                {
                    AnimatedProgressStart = 0.0;
                    AnimatedProgressEnd = RangeWidth;
                }

                // Notify property changes
                ProgressChanged?.Invoke();
            });
        };
        progressAnimationTimer.AutoReset = true;
        progressAnimationTimer.Start();
    }

    public void Stop()
    {
        if (progressAnimationTimer != null)
        {
            progressAnimationTimer.Stop();
            progressAnimationTimer.Dispose();
            progressAnimationTimer = null;
            AnimatedProgressStart = 0.0;
            AnimatedProgressEnd = RangeWidth;
            ProgressChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}

