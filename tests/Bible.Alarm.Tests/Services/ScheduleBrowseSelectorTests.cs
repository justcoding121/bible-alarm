#nullable enable

using Bible.Alarm.Services.Media.BrowseHelpers;

namespace Bible.Alarm.Tests.Services;

public sealed class ScheduleBrowseSelectorTests
{
    [Fact]
    public void Select_uses_fluxor_when_it_has_schedules()
    {
        var chosen = ScheduleBrowseSelector.Select(new[] { 4 }, new[] { 9 });

        Assert.Equal(new[] { 4 }, chosen);
    }

    [Fact]
    public void Select_uses_the_database_when_fluxor_is_empty()
    {
        var chosen = ScheduleBrowseSelector.Select(Array.Empty<int>(), new[] { 9, 10 });

        Assert.Equal(new[] { 9, 10 }, chosen);
    }
}

public sealed class FutureCompletionTests
{
    [Fact]
    public void TryComplete_returns_true_when_set_succeeds()
    {
        var failed = false;
        var completed = FutureCompletion.TryComplete(() => true, _ => failed = true);

        Assert.True(completed);
        Assert.False(failed);
    }

    [Fact]
    public void TryComplete_reports_a_false_set_and_a_thrown_set()
    {
        Exception? reported = null;
        Assert.False(FutureCompletion.TryComplete(() => false, error => reported = error));
        Assert.Null(reported);

        var thrown = FutureCompletion.TryComplete(
            () => throw new InvalidOperationException("jni"),
            error => reported = error);

        Assert.False(thrown);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public void Browse_wait_is_shorter_than_a_minute()
    {
        Assert.InRange(FutureCompletion.BrowseBootstrapWaitMs, 1, 15_000);
    }
}
