#nullable enable

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages progress bar visibility and opacity for HomeViewModel.
/// Animation is now handled natively by the AnimatedProgressBar control.
/// </summary>
public class ProgressBarManager : IDisposable
{
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

    public bool IsProgressBarHidden => progressBarOpacity == 0;

    // These properties are kept for backwards compatibility but no longer used
    // Animation is now handled natively in AnimatedProgressBar control
    public double AnimatedProgressStart => 0.0;
    public double AnimatedProgressEnd => 0.3;
    public double AnimatedProgress => 0.3;
    public double AnimatedProgressRangeWidth => 0.3;

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

        // If already hidden, don't re-animate (would cause a flash)
        if (progressBarOpacity == 0)
        {
            return;
        }

        const int fadeSteps = 10;
        const int fadeDurationMs = 200;
        const double stepDelay = fadeDurationMs / (double)fadeSteps;

        // Fade from current opacity to 0 (not from 1.0)
        var startOpacity = progressBarOpacity;
        for (int i = fadeSteps; i >= 0; i--)
        {
            ProgressBarOpacity = startOpacity * i / fadeSteps;
            await Task.Delay((int)stepDelay);
        }

        ProgressBarOpacity = 0.0;
    }

    /// <summary>
    /// Hides the progress bar with fade animation without affecting the shouldShowProgressBar flag.
    /// Used for hiding progress after next/prev track operations complete.
    /// </summary>
    public async Task HideTemporarilyAsync()
    {
        // If already hidden, don't re-animate (would cause a flash)
        if (progressBarOpacity == 0)
        {
            return;
        }

        const int fadeSteps = 10;
        const int fadeDurationMs = 200;
        const double stepDelay = fadeDurationMs / (double)fadeSteps;

        // Fade from current opacity to 0 (not from 1.0)
        var startOpacity = progressBarOpacity;
        for (int i = fadeSteps; i >= 0; i--)
        {
            ProgressBarOpacity = startOpacity * i / fadeSteps;
            await Task.Delay((int)stepDelay);
        }

        ProgressBarOpacity = 0.0;
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

