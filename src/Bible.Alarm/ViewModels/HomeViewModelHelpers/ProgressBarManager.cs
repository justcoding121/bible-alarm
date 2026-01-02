#nullable enable

using Bible.Alarm.ViewModels.Services.Home;
using Microsoft.Maui.Essentials;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages progress bar visibility, opacity, and animation for HomeViewModel.
/// </summary>
public class ProgressBarManager
{
    private readonly ProgressBarAnimator progressAnimator;
    private bool shouldShowProgressBar = true;
    private double progressBarOpacity = 1.0;

    public ProgressBarManager(ProgressBarAnimator progressAnimator)
    {
        this.progressAnimator = progressAnimator;
        progressAnimator.ProgressChanged += OnProgressChanged;
    }

    public event Action<double>? ProgressBarOpacityChanged;
    public event Action? ProgressBarHiddenChanged;

    public double ProgressBarOpacity
    {
        get => progressBarOpacity;
        set
        {
            if (progressBarOpacity != value)
            {
                progressBarOpacity = value;
                ProgressBarOpacityChanged?.Invoke(value);
                ProgressBarHiddenChanged?.Invoke();
            }
        }
    }

    public bool IsProgressBarHidden => progressBarOpacity == 0;
    public double AnimatedProgressStart => progressAnimator.AnimatedProgressStart;
    public double AnimatedProgressEnd => progressAnimator.AnimatedProgressEnd;
    public double AnimatedProgress => progressAnimator.AnimatedProgress;
    public double AnimatedProgressRangeWidth => progressAnimator.AnimatedProgressRangeWidth;

    public void UpdateVisibility(bool isBusy, int? schedulesCount)
    {
        var shouldShow = shouldShowProgressBar && (isBusy || (schedulesCount == null || schedulesCount == 0));
        ProgressBarOpacity = shouldShow ? 1.0 : 0.0;

        if (shouldShow)
        {
            progressAnimator.Start();
        }
        else
        {
            progressAnimator.Stop();
        }
    }

    public async Task FadeOutAsync()
    {
        shouldShowProgressBar = false;

        const int fadeSteps = 10;
        const int fadeDurationMs = 200;
        const double stepDelay = fadeDurationMs / (double)fadeSteps;
        const double opacityStep = 1.0 / fadeSteps;

        for (int i = fadeSteps; i >= 0; i--)
        {
            ProgressBarOpacity = i * opacityStep;
            await Task.Delay((int)stepDelay);
        }

        ProgressBarOpacity = 0.0;
    }

    public void Reset()
    {
        shouldShowProgressBar = true;
    }

    private void OnProgressChanged()
    {
        ProgressBarOpacityChanged?.Invoke(progressBarOpacity);
    }

    public void Dispose()
    {
        progressAnimator.ProgressChanged -= OnProgressChanged;
        progressAnimator.Dispose();
    }
}

