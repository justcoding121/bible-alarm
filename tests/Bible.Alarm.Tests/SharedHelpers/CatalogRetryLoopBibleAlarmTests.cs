#nullable enable

using System.Net.Http;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class CatalogRetryLoopBibleAlarmTests
{
    [Fact]
    public async Task RunAsync_returns_success_on_first_completed_iteration()
    {
        var result = await CatalogRetryLoop.RunAsync(
            _ => Task.FromResult(CatalogRetryIterationResult<string>.Success("cataloged")));

        Assert.True(result.AllCataloged);
        Assert.Equal("cataloged", result.Data);
    }

    [Fact]
    public async Task RunAsync_rethrows_network_failures()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CatalogRetryLoop.RunAsync<string>(_ => throw new HttpRequestException("offline")));
    }
}
