#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class FetchProgressReporterTests
{
    [Fact]
    public void CategoryFetchProgressReporter_UpdateProgress_clamps_and_does_not_throw()
    {
        var sut = new CategoryFetchProgressReporter(42);

        sut.UpdateProgress(-1);
        sut.UpdateProgress(2);
        sut.UpdateProgress(0.5);

        Assert.False(sut.CancellationToken.CanBeCanceled);
    }

    [Fact]
    public void CategoryFetchProgressReporter_ctor_stores_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        var sut = new CategoryFetchProgressReporter(9, cts.Token);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    [Fact]
    public void CategoryFetchProgressReporter_UpdateProgressText_and_SetIsVisible_are_no_ops()
    {
        var sut = new CategoryFetchProgressReporter(1);

        sut.UpdateProgressText("ignored");
        sut.SetIsVisible(true);
    }

    [Fact]
    public void SectionFetchProgressReporter_UpdateProgress_clamps_and_does_not_throw()
    {
        using var cts = new CancellationTokenSource();
        var sut = new SectionFetchProgressReporter(cts.Token);

        sut.UpdateProgress(-0.5);
        sut.UpdateProgress(3);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    [Fact]
    public void SectionFetchProgressReporter_UpdateProgressText_and_SetIsVisible_no_op()
    {
        var sut = new SectionFetchProgressReporter(default);

        sut.UpdateProgressText("ignored");
        sut.SetIsVisible(false);
    }

    [Fact]
    public void ModalOverlayFetchProgressReporter_UpdateProgress_clamps_and_does_not_throw()
    {
        using var cts = new CancellationTokenSource();
        var sut = new ModalOverlayFetchProgressReporter("BiblePublication", cts.Token);

        sut.UpdateProgress(-1);
        sut.UpdateProgress(2);
        sut.UpdateProgress(0.5);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    [Fact]
    public void ModalOverlayFetchProgressReporter_UpdateProgressText_and_SetIsVisible_do_not_throw()
    {
        var sut = new ModalOverlayFetchProgressReporter("BibleSection", default);

        sut.UpdateProgressText("Fetching...");
        sut.SetIsVisible(true);
        sut.SetIsVisible(false);
    }

    [Fact]
    public void ListItemFetchProgressReporter_UpdateProgress_invokes_callback_and_clamps()
    {
        var calls = 0;
        var sut = new ListItemFetchProgressReporter("BiblePublication", "pub-1", () => calls++, default);

        sut.UpdateProgress(2);
        sut.UpdateProgress(-0.5);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void ListItemFetchProgressReporter_no_ops_for_text_and_visibility()
    {
        var sut = new ListItemFetchProgressReporter("ctx", "id");

        sut.UpdateProgressText("ignored");
        sut.SetIsVisible(true);
    }

    [Fact]
    public void ListItemFetchProgressReporter_UpdateProgress_without_callback_clamps_and_does_not_throw()
    {
        var sut = new ListItemFetchProgressReporter("ctx", "id", onProgressReported: null);

        var ex = Record.Exception(() =>
        {
            sut.UpdateProgress(0);
            sut.UpdateProgress(1);
            sut.UpdateProgress(1.25);
            sut.UpdateProgress(-0.1);
        });

        Assert.Null(ex);
    }
}
