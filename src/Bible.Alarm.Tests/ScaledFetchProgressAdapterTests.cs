#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class ScaledFetchProgressAdapterTests
{
    private sealed class RecordingFetchProgress : IFetchProgress
    {
        public List<double> ProgressValues { get; } = [];

        public CancellationToken CancellationToken => CancellationToken.None;

        public void UpdateProgress(double progress) => ProgressValues.Add(progress);

        public void UpdateProgressText(string text)
        {
        }

        public void SetIsVisible(bool isVisible)
        {
        }
    }

    [Fact]
    public void Constructor_throws_when_target_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ScaledFetchProgressAdapter(null!, 0.5));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    public void Constructor_clamps_scale(double scale, double expectedEffectiveScale)
    {
        var inner = new RecordingFetchProgress();
        var sut = new ScaledFetchProgressAdapter(inner, scale);

        sut.UpdateProgress(1.0);

        Assert.Equal(expectedEffectiveScale, Assert.Single(inner.ProgressValues));
    }

    [Fact]
    public void UpdateProgress_clamps_input_then_multiplies_by_scale()
    {
        var inner = new RecordingFetchProgress();
        var sut = new ScaledFetchProgressAdapter(inner, 0.5);

        sut.UpdateProgress(0.8);

        Assert.Equal(0.4, Assert.Single(inner.ProgressValues));

        inner.ProgressValues.Clear();
        sut.UpdateProgress(3.0);

        Assert.Equal(0.5, Assert.Single(inner.ProgressValues));
    }

    [Fact]
    public void Proxies_text_visibility_and_cancellation_token()
    {
        var inner = new RecordingFetchProgress();
        var sut = new ScaledFetchProgressAdapter(inner, 1.0);

        Assert.Equal(inner.CancellationToken, sut.CancellationToken);

        sut.UpdateProgressText("x");
        sut.SetIsVisible(false);
    }
}
