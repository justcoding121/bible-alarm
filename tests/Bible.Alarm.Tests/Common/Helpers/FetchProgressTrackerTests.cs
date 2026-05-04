#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class FetchProgressTrackerTests
{
    [Fact]
    public void UpdateProgress_clamps_and_updates_progress_and_auto_text()
    {
        double? progress = null;
        string? text = null;
        using var cts = new CancellationTokenSource();
        var sut = new FetchProgressTracker(
            d => progress = d,
            t => text = t,
            _ => { },
            cts.Token);

        sut.UpdateProgress(-0.25);
        sut.UpdateProgress(2);

        Assert.Equal(1.0, progress);
        Assert.Equal("100%", text);
        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    [Fact]
    public void UpdateProgressText_and_SetIsVisible_invoke_callbacks()
    {
        var texts = new List<string>();
        var visible = new List<bool>();
        var sut = new FetchProgressTracker(_ => { }, t => texts.Add(t), visible.Add, default);

        sut.UpdateProgressText("busy");
        sut.SetIsVisible(false);

        Assert.Equal(["busy"], texts);
        Assert.Equal([false], visible);
    }

    [Fact]
    public void UpdateProgress_percent_rounding_uses_MathRound_default_semantics_for_half_edges()
    {
        string? text = null;
        var sut = new FetchProgressTracker(_ => { }, t => text = t, _ => { });

        sut.UpdateProgress(0.505);

        Assert.Equal("50%", text);
        sut.UpdateProgress(0.515);
        Assert.Equal("52%", text);
    }

    [Fact]
    public void UpdateProgress_swallows_throw_from_callbacks()
    {
        var sut = new FetchProgressTracker(
            _ => throw new InvalidOperationException("progress"),
            _ => throw new InvalidOperationException("text"),
            _ => throw new InvalidOperationException("visible"));

        sut.UpdateProgress(0.42);
    }

    [Fact]
    public void UpdateProgressText_swallows_throw_from_callback()
    {
        var sut = new FetchProgressTracker(
            _ => { },
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException());

        sut.UpdateProgressText("try");
    }

    [Fact]
    public void SetIsVisible_swallows_throw_from_callback()
    {
        var sut = new FetchProgressTracker(
            _ => { },
            _ => { },
            _ => throw new InvalidOperationException());

        sut.SetIsVisible(true);
    }
}
