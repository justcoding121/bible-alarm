#nullable enable

using System.Net.Http;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class CatalogRetryLoopTests
{
    [Fact]
    public void DelayMilliseconds_caps_at_five_seconds()
    {
        Assert.Equal(1000, CatalogRetryLoop.DelayMilliseconds(1));
        Assert.Equal(4000, CatalogRetryLoop.DelayMilliseconds(4));
        Assert.Equal(5000, CatalogRetryLoop.DelayMilliseconds(5));
        Assert.Equal(5000, CatalogRetryLoop.DelayMilliseconds(12));
    }

    [Fact]
    public async Task RunAsync_returns_success_snapshot_on_first_completed_iteration()
    {
        var result = await CatalogRetryLoop.RunAsync(
            _ => Task.FromResult(CatalogRetryIterationResult<string>.Success("cataloged")));

        Assert.True(result.AllCataloged);
        Assert.Equal(1, result.Attempt);
        Assert.Equal("cataloged", result.Data);
    }

    [Fact]
    public async Task RunAsync_stops_when_iteration_reports_stagnation()
    {
        var result = await CatalogRetryLoop.RunAsync(
            _ => Task.FromResult(CatalogRetryIterationResult<string>.Stagnation("partial")));

        Assert.False(result.AllCataloged);
        Assert.Equal(1, result.Attempt);
        Assert.Equal("partial", result.Data);
    }

    [Fact]
    public async Task RunAsync_passes_updated_cataloged_count_to_the_next_iteration()
    {
        var seenPrevious = new List<int>();
        var result = await CatalogRetryLoop.RunAsync<string>(async ctx =>
        {
            seenPrevious.Add(ctx.PreviousCatalogedCount);
            if (ctx.Attempt == 1)
            {
                return CatalogRetryIterationResult<string>.Continue("first", 2);
            }

            return CatalogRetryIterationResult<string>.Success("done");
        });

        Assert.True(result.AllCataloged);
        Assert.Equal(2, result.Attempt);
        Assert.Equal([-1, 2], seenPrevious);
        Assert.Equal("done", result.Data);
    }

    [Fact]
    public async Task RunAsync_retries_garden_variety_errors_then_succeeds()
    {
        var retryableLogged = 0;
        var result = await CatalogRetryLoop.RunAsync<string>(
            ctx =>
            {
                if (ctx.Attempt == 1)
                {
                    throw new InvalidOperationException("transient");
                }

                return Task.FromResult(CatalogRetryIterationResult<string>.Success("recovered"));
            },
            (_, _) => retryableLogged++);

        Assert.True(result.AllCataloged);
        Assert.Equal(2, result.Attempt);
        Assert.Equal(1, retryableLogged);
        Assert.Equal("recovered", result.Data);
    }

    [Fact]
    public async Task RunAsync_rethrows_network_failures_without_retrying()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CatalogRetryLoop.RunAsync<string>(_ => throw new HttpRequestException("offline")));
    }

    [Fact]
    public async Task RunAsync_stops_after_max_retries_when_iterations_never_complete()
    {
        var attempts = 0;
        var result = await CatalogRetryLoop.RunAsync(
            ctx =>
            {
                attempts++;
                return Task.FromResult(CatalogRetryIterationResult<int>.Continue(ctx.Attempt, ctx.Attempt));
            });

        Assert.False(result.AllCataloged);
        Assert.Equal(CatalogRetryLoop.MaxRetries, result.Attempt);
        Assert.Equal(CatalogRetryLoop.MaxRetries, attempts);
        Assert.Equal(CatalogRetryLoop.MaxRetries, result.Data);
    }

    [Fact]
    public async Task RunAsync_throws_when_cancelled_before_an_iteration()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CatalogRetryLoop.RunAsync(
                _ => Task.FromResult(CatalogRetryIterationResult<string>.Success("unused")),
                cancellationToken: cts.Token));
    }
}
