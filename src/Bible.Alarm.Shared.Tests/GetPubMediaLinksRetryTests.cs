using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class GetPubMediaLinksRetryTests
{
    [Fact]
    public async Task GetStringAsync_ReturnsNull_WhenBaseUrlListEmpty()
    {
        using var client = new HttpClient(new ThrowHandler());
        var result = await GetPubMediaLinksRetry.GetStringAsync(client, Array.Empty<string>(), "?q=1");
        Assert.Null(result);
    }

    [Fact]
    public void GetBaseUrlsFromConstants_ReturnsConfiguredEndpoints()
    {
        var urls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        Assert.NotEmpty(urls);
        Assert.All(urls, u => Assert.False(string.IsNullOrWhiteSpace(u)));
    }

    [Fact]
    public async Task GetStringAsync_OnSingleHost_RetriesUntilSuccess_OnThirdAttempt()
    {
        var calls = 0;
        using var inner = new CallbackHandler(n =>
        {
            calls = n;
            if (n < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ \"ok\": true }")
            };
        });

        using var client = new HttpClient(inner);

        var result = await GetPubMediaLinksRetry.GetStringAsync(
            client,
            new[] { "https://stub.local" },
            "/path?x=1");

        Assert.Equal("{ \"ok\": true }", result);
        Assert.Equal(3, calls);
        Assert.StartsWith("https://stub.local/path?x=1", inner.LastRequestUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStringAsync_OnSingleHost_ReturnsNull_WhenNeverSuccessful()
    {
        using var inner = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(inner);

        var result = await GetPubMediaLinksRetry.GetStringAsync(
            client,
            new[] { "https://stub.local" },
            "?");

        Assert.Null(result);
    }

    private sealed class CallbackHandler(Func<int, HttpResponseMessage> onCall) : HttpMessageHandler
    {
        private int _callSeq;
        public string LastRequestUri { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _callSeq);
            LastRequestUri = request.RequestUri?.ToString() ?? "";
            return Task.FromResult(onCall(n));
        }
    }

    private sealed class ThrowHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("unexpected HTTP call");
    }
}
