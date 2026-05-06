#nullable enable

using System.Diagnostics;
using System.Windows.Input;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class AlarmViewModalSliderHandlerTests
{
    /// <summary>
    /// AlarmViewModalSliderHandler ends interaction via <c>Task.Delay(500).ContinueWith(...)</c> on the
    /// thread pool. Under CI load the continuation can be scheduled noticeably after the 500 ms delay,
    /// so a single fixed sleep (e.g. 1500 ms) flakes. Poll until the predicate holds or timeout.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan pollInterval)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.Elapsed > timeout)
            {
                Assert.True(false, $"Condition not satisfied within {timeout.TotalMilliseconds} ms (elapsed {sw.ElapsedMilliseconds} ms).");
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }
    }

    private sealed class RecordingSeekCommand : ICommand
    {
        public List<TimeSpan> Executed { get; } = [];
        public bool CanExecuteResult { get; set; } = true;
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => CanExecuteResult;

        public void Execute(object? parameter)
        {
            if (parameter is TimeSpan ts)
            {
                Executed.Add(ts);
            }
        }
    }

    private sealed class ThrowingSeekCommand : ICommand
    {
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) =>
            throw new InvalidOperationException("seek failed");

#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void OnSliderTapped_WhenControlsDisabled_DoesNothing()
    {
        List<double> progress = [];
        var notifyCount = 0;
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => false,
            () => TimeSpan.FromSeconds(60),
            p => progress.Add(p),
            () => notifyCount++);

        handler.OnSliderTapped(0.5);

        Assert.Empty(progress);
        Assert.Equal(0, notifyCount);
        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderTapped_WhenDurationZero_DoesNothing()
    {
        List<double> progress = [];
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.Zero,
            progress.Add,
            () => { });

        handler.OnSliderTapped(0.5);

        Assert.Empty(progress);
        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderTapped_ClampsProgressAndStartsInteraction()
    {
        List<double> progress = [];
        var notifies = 0;
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(100),
            progress.Add,
            () => notifies++);
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(1.5);

        Assert.Equal(1.0, Assert.Single(progress));
        Assert.Equal(1, notifies);
        Assert.True(handler.IsUserInteracting);
        var expected = TimeSpan.FromSeconds(100);
        Assert.Equal(expected, Assert.Single(command.Executed));
    }

    [Fact]
    public void OnSliderTapped_NegativeValue_ClampedToZero()
    {
        List<double> progress = [];
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(40),
            progress.Add,
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(-0.5);

        Assert.Equal(0.0, Assert.Single(progress));
        Assert.Equal(TimeSpan.Zero, Assert.Single(command.Executed));
    }

    [Fact]
    public void OnSliderDragStarted_SetsUserInteracting()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(10),
            _ => { },
            () => { });

        handler.OnSliderDragStarted();

        Assert.True(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderDragCompleted_WhenControlsDisabled_ClearsInteraction()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => false,
            () => TimeSpan.FromSeconds(10),
            _ => { },
            () => { });
        handler.OnSliderDragStarted();

        handler.OnSliderDragCompleted(0.3);

        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderDragCompleted_WhenEligible_SeeksAtClampedProgress()
    {
        List<double> progress = [];
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(80),
            progress.Add,
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderDragCompleted(0.25);

        Assert.Equal(0.25, Assert.Single(progress));
        Assert.Equal(TimeSpan.FromSeconds(20), Assert.Single(command.Executed));
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_WhenNotInteracting_ReturnsFalse()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(50),
            _ => { },
            () => { });

        Assert.False(handler.ShouldIgnorePositionUpdate(0.3));
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_WhenCloseToTarget_EndsInteraction()
    {
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(100),
            _ => { },
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(0.5);
        Assert.True(handler.IsUserInteracting);

        Assert.False(handler.ShouldIgnorePositionUpdate(0.51));

        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_WhenFarFromTarget_ReturnsTrueWhileInteracting()
    {
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(100),
            _ => { },
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(0.5);

        Assert.True(handler.ShouldIgnorePositionUpdate(0.0));
        Assert.True(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderTapped_WhenSeekCommandCannotExecute_LogsAndStillDelaysInteractionEnd()
    {
        var command = new RecordingSeekCommand { CanExecuteResult = false };
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(100),
            _ => { },
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(0.4);

        Assert.Empty(command.Executed);
        Assert.True(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderDragCompleted_WhenDurationZero_ClearsInteraction()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.Zero,
            _ => { },
            () => { });
        handler.OnSliderDragStarted();

        handler.OnSliderDragCompleted(0.3);

        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public async Task OnSliderTapped_WithoutSeekCommand_EndsInteractionAfterDelay()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(60),
            _ => { },
            () => { });

        handler.OnSliderTapped(0.3);

        Assert.True(handler.IsUserInteracting);
        await WaitUntilAsync(() => !handler.IsUserInteracting, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_when_duration_becomes_zero_does_not_match_target_to_end_early()
    {
        var durationBox = new[] { TimeSpan.FromSeconds(100) };
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => durationBox[0],
            _ => { },
            () => { });
        var command = new RecordingSeekCommand();
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(0.5);

        durationBox[0] = TimeSpan.Zero;

        Assert.True(handler.ShouldIgnorePositionUpdate(0.5));
        Assert.True(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderDragCompleted_NegativeValue_ClampedToZero()
    {
        List<double> progress = [];
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(50),
            progress.Add,
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderDragCompleted(-0.25);

        Assert.Equal(0.0, Assert.Single(progress));
        Assert.Equal(TimeSpan.Zero, Assert.Single(command.Executed));
    }

    [Fact]
    public async Task OnSliderTapped_AfterDelay_EndsUserInteraction()
    {
        var command = new RecordingSeekCommand();
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(50),
            _ => { },
            () => { });
        handler.SetSeekCommand(command);

        handler.OnSliderTapped(0.2);

        await WaitUntilAsync(() => !handler.IsUserInteracting, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderTapped_WhenSeekCommandExecuteThrows_resets_interaction()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(30),
            _ => { },
            () => { });
        handler.SetSeekCommand(new ThrowingSeekCommand());

        var ex = Record.Exception(() => handler.OnSliderTapped(0.5));

        Assert.Null(ex);
        Assert.False(handler.IsUserInteracting);
    }

    [Fact]
    public void OnSliderDragCompleted_WhenSeekCommandExecuteThrows_resets_interaction()
    {
        var handler = new AlarmViewModalSliderHandler(
            TestLogging.CreateLogger(),
            () => true,
            () => TimeSpan.FromSeconds(45),
            _ => { },
            () => { });
        handler.SetSeekCommand(new ThrowingSeekCommand());

        var ex = Record.Exception(() => handler.OnSliderDragCompleted(0.33));

        Assert.Null(ex);
        Assert.False(handler.IsUserInteracting);
    }
}
