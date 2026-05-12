#nullable enable

using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ProgressBarManagerTests
{
    [Fact]
    public void ProgressBarOpacity_change_outside_tolerance_invokes_subscribers_once_per_distinct_value()
    {
        var invokesOpacity = new List<double>();
        var invokesHidden = 0;

        var sut = new ProgressBarManager(null!);
        sut.ProgressBarOpacityChanged += v => invokesOpacity.Add(v);
        sut.ProgressBarHiddenChanged += () => invokesHidden++;

        sut.ProgressBarOpacity = 0;

        sut.ProgressBarOpacity = 1.0;

        sut.ProgressBarOpacity = 0.42;
        sut.ProgressBarOpacity = 0.419999;

        sut.ProgressBarOpacity = 1.0;
        sut.ProgressBarOpacity = 1.0;

        Assert.Equal(new double[] { 0.0, 1.0, 0.42, 1.0 }, invokesOpacity);
        Assert.Equal(4, invokesHidden);
        Assert.False(sut.IsProgressBarHidden);
    }

    [Fact]
    public void IsProgressBarHidden_true_when_opacity_is_effectively_zero()
    {
        var sut = new ProgressBarManager(null!);

        sut.ProgressBarOpacity = 5e-10;

        Assert.True(sut.IsProgressBarHidden);
    }

    [Fact]
    public void AnimatedProgress_constants_cover_expected_segment()
    {
        Assert.Equal(0.3, ProgressBarManager.AnimatedProgressEnd);
        Assert.Equal(ProgressBarManager.AnimatedProgress, ProgressBarManager.AnimatedProgressEnd);
        Assert.Equal(0.3, ProgressBarManager.AnimatedProgressRangeWidth);
    }

    [Fact]
    public void UpdateVisibility_hides_when_not_busy_and_schedules_loaded()
    {
        var invokes = new List<double>();
        var sut = new ProgressBarManager(null!);
        sut.ProgressBarOpacityChanged += invokes.Add;

        sut.UpdateVisibility(isBusy: false, schedulesCount: 4);

        Assert.Single(invokes);
        Assert.Equal(0.0, invokes[0]);
        Assert.True(sut.IsProgressBarHidden);
    }

    [Fact]
    public async Task FadeOutAsync_precludes_future_visibility_updates_from_showing_bar()
    {
        var sut = new ProgressBarManager(null!);

        await sut.FadeOutAsync();

        sut.UpdateVisibility(isBusy: true, schedulesCount: null);

        Assert.True(sut.IsProgressBarHidden);
    }

    [Fact]
    public async Task HideTemporarilyAsync_hides_opaque_bar_quickly_without_touching_visibility_flag()
    {
        var sut = new ProgressBarManager(null!);

        await sut.HideTemporarilyAsync();

        Assert.True(sut.IsProgressBarHidden);
        sut.UpdateVisibility(isBusy: true, schedulesCount: null);
        Assert.False(sut.IsProgressBarHidden);
    }

    [Fact]
    public async Task HideTemporarilyAsync_early_exit_keeps_already_hidden_bar()
    {
        var sut = new ProgressBarManager(null!);

        sut.ProgressBarOpacity = 0;

        await sut.HideTemporarilyAsync();

        Assert.True(sut.IsProgressBarHidden);
    }

    [Fact]
    public async Task Reset_restores_visibility_after_FadeOut_for_loading_style_states()
    {
        var invokes = new List<double>();
        var sut = new ProgressBarManager(null!);
        sut.ProgressBarOpacityChanged += invokes.Add;

        await sut.FadeOutAsync();
        invokes.Clear();

        sut.Reset();

        sut.UpdateVisibility(isBusy: false, schedulesCount: null);

        Assert.Single(invokes);
        Assert.Equal(1.0, invokes[0]);
        Assert.False(sut.IsProgressBarHidden);
    }
}
