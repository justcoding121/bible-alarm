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
    public void SectionFetchProgressReporter_UpdateProgress_clamps_and_does_not_throw()
    {
        using var cts = new CancellationTokenSource();
        var sut = new SectionFetchProgressReporter(cts.Token);

        sut.UpdateProgress(-0.5);
        sut.UpdateProgress(3);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }
}
