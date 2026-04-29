#nullable enable

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages progress bar visibility and opacity for HomeViewModel.
/// Animation is now handled natively by the AnimatedProgressBar control.
/// </summary>
public class ProgressBarManager : IDisposable
{
    private const double OpacityEpsilon = 1e-9;
    private bool shouldShowProgressBar = true;
    private double progressBarOpacity = 1.0;

    public ProgressBarManager(ProgressBarAnimator progressAnimator)
    {
        // ProgressBarAnimator is kept for backwards compatibility but animation
        // is now handled natively in AnimatedProgressBar.xaml.cs
    }

    public event Action<double>? ProgressBarOpacityChanged;
    public event Action? ProgressBarHiddenChanged;

    public double ProgressBarOpacity
    {
        get => progressBarOpacity;
        set
        {
            if (Math.Abs(progressBarOpacity - value) > 0.001)
            {
                progressBarOpacity = value;
                ProgressBarOpacityChanged?.Invoke(value);
                ProgressBarHiddenChanged?.Invoke();
            }
        }
    }

    public bool IsProgressBarHidden => Math.Abs(progressBarOpacity) < OpacityEpsilon;

    // These properties are kept for backwards compatibility but no longer used
    // Animation is now handled natively in AnimatedProgressBar control
    public static double AnimatedProgressStart => 0.0;
    public static double AnimatedProgressEnd => 0.3;
    public static double AnimatedProgress => 0.3;
    public static double AnimatedProgressRangeWidth => 0.3;

    public void UpdateVisibility(bool isBusy, int? schedulesCount)
    {
        var shouldShow = shouldShowProgressBar && (isBusy || (schedulesCount == null || schedulesCount == 0));
        ProgressBarOpacity = shouldShow ? 1.0 : 0.0;
    }

    /// <summary>
    /// Temporarily shows the progress bar without affecting the shouldShowProgressBar flag.
    /// Used for showing progress during next/prev track operations.
    /// </summary>
    public void ShowTemporarily()
    {
        ProgressBarOpacity = 1.0;
    }

    public async Task FadeOutAsync()
    {
        // Prevent showing progress bar again
        shouldShowProgressBar = false;

        if (Math.Abs(progressBarOpacity) < OpacityEpsilon)
        {
            return;
        }

        // Hide immediately instead of using an animation loop
        // The Task.Delay-based animation loop can get blocked when the main thread
        // is busy with heavy UI work (assembly loading, schedule rendering, etc.),
        // causing the progress bar to appear "stuck" for several seconds.
        // Immediate hide is more reliable and provides a snappier UX.
        ProgressBarOpacity = 0.0;

        // Yield to allow UI to process the opacity change
        await Task.Yield();
    }

    /// <summary>
    /// Hides the progress bar immediately without affecting the shouldShowProgressBar flag.
    /// Used for hiding progress after next/prev track operations complete.
    /// </summary>
    public async Task HideTemporarilyAsync()
    {
        if (Math.Abs(progressBarOpacity) < OpacityEpsilon)
        {
            return;
        }

        // Hide immediately for a snappier UX
        ProgressBarOpacity = 0.0;

        // Yield to allow UI to process the opacity change
        await Task.Yield();
    }

    public void Reset()
    {
        shouldShowProgressBar = true;
    }

    public void Dispose()
    {
        // No resources to dispose - animation is handled by the view
    }
}

