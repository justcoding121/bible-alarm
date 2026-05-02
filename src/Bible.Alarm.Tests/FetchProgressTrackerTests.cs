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
}
