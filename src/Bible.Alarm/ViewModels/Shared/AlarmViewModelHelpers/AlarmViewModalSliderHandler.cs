#nullable enable
using System.Windows.Input;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

public class AlarmViewModalSliderHandler
{
    private readonly ILogger logger;
    private ICommand? seekCommand;
    private readonly Func<bool> areControlsEnabled;
    private readonly Func<TimeSpan> getCurrentDuration;
    private readonly Action<double> setProgressDirectly;
    private readonly Action notifyProgressChanged;

    private bool isUserInteracting;
    private double? targetSeekProgress;

    public AlarmViewModalSliderHandler(
        ILogger logger,
        Func<bool> areControlsEnabled,
        Func<TimeSpan> getCurrentDuration,
        Action<double> setProgressDirectly,
        Action notifyProgressChanged)
    {
        this.logger = logger;
        this.areControlsEnabled = areControlsEnabled;
        this.getCurrentDuration = getCurrentDuration;
        this.setProgressDirectly = setProgressDirectly;
        this.notifyProgressChanged = notifyProgressChanged;
    }

    public void SetSeekCommand(ICommand command)
    {
        seekCommand = command;
    }

    public bool IsUserInteracting => isUserInteracting;

    public void OnSliderTapped(double targetValue)
    {
        logger.Debug("[Slider] Tap detected - TargetValue: {TargetValue}", targetValue);

        if (!areControlsEnabled() || getCurrentDuration().TotalSeconds <= 0)
        {
            return;
        }

        var progress = Math.Max(0.0, Math.Min(1.0, targetValue));
        targetSeekProgress = progress;

        isUserInteracting = true;
        setProgressDirectly(progress);
        notifyProgressChanged();

        PerformSeek();
    }

    public void OnSliderDragStarted()
    {
        isUserInteracting = true;
        logger.Debug("[Slider] Drag started");
    }

    public void OnSliderDragCompleted(double finalValue)
    {
        logger.Debug("[Slider] Drag completed - FinalValue: {FinalValue}", finalValue);

        if (!areControlsEnabled() || getCurrentDuration().TotalSeconds <= 0)
        {
            isUserInteracting = false;
            return;
        }

        var progress = Math.Max(0.0, Math.Min(1.0, finalValue));
        targetSeekProgress = progress;

        setProgressDirectly(progress);
        notifyProgressChanged();

        PerformSeek();
    }

    public bool ShouldIgnorePositionUpdate(double actualProgress)
    {
        if (!isUserInteracting)
        {
            return false;
        }

        if (targetSeekProgress.HasValue && getCurrentDuration().TotalSeconds > 0)
        {
            var progressDiff = Math.Abs(actualProgress - targetSeekProgress.Value);

            if (progressDiff < 0.02)
            {
                isUserInteracting = false;
                targetSeekProgress = null;
                return false;
            }
        }

        return true;
    }

    private void PerformSeek()
    {
        if (!targetSeekProgress.HasValue || !areControlsEnabled() || getCurrentDuration().TotalSeconds <= 0)
        {
            isUserInteracting = false;
            targetSeekProgress = null;
            return;
        }

        try
        {
            var seekPosition = TimeSpan.FromSeconds(getCurrentDuration().TotalSeconds * targetSeekProgress.Value);
            logger.Debug("[Slider] Performing seek to position: {Position}, Progress: {Progress}",
                seekPosition, targetSeekProgress.Value);

            if (seekCommand != null)
            {
                if (seekCommand.CanExecute(seekPosition))
                {
                    seekCommand.Execute(seekPosition);
                }
                else
                {
                    logger.Warning("[Slider] SeekCommand.CanExecute returned false - command may still be running from a previous invocation");
                }
            }

            Task.Delay(500).ContinueWith(_ =>
            {
                isUserInteracting = false;
                targetSeekProgress = null;
                logger.Debug("[Slider] User interaction ended");
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[Slider] Error performing seek");
            isUserInteracting = false;
            targetSeekProgress = null;
        }
    }
}

