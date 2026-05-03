using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class GetPubMediaLinksRetryTests
{
    private static readonly string[] StubLocalBaseUrls = ["https://stub.local"];

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
            StubLocalBaseUrls,
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
            StubLocalBaseUrls,
            "?");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetStringAsync_WithNonMaterializedEnumerableBaseUrls_SelectsSuccessfully()
    {
        using var inner = new CallbackHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ }")
        });
        using var client = new HttpClient(inner);

        var result = await GetPubMediaLinksRetry.GetStringAsync(
            client,
            TwoYieldedBaseUrls(),
            "/q");

        Assert.Equal("{ }", result);
    }

    [Fact]
    public async Task GetStringAsync_Retries_AfterHttpRequestException()
    {
        var calls = 0;
        using var inner = new HttpMessageHandlerExceptionThenOk(onAttempt: attempt =>
        {
            calls++;
            if (attempt < 2)
            {
                throw new HttpRequestException("simulated");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("recovered")
            };
        });

        using var client = new HttpClient(inner);

        var result = await GetPubMediaLinksRetry.GetStringAsync(client, StubLocalBaseUrls, "?x=y");

        Assert.Equal("recovered", result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetStringAsync_Retries_AfterTaskCanceled_WhenCancellationNotRequested()
    {
        var calls = 0;
        using var inner = new HttpMessageHandlerExceptionThenOk(onAttempt: attempt =>
        {
            calls++;
            if (attempt == 1)
            {
                throw new TaskCanceledException("simulated timeout");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("after-timeout")
            };
        });

        using var client = new HttpClient(inner);

        var result = await GetPubMediaLinksRetry.GetStringAsync(client, StubLocalBaseUrls, "?z=1");

        Assert.Equal("after-timeout", result);
        Assert.Equal(2, calls);
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

    private static IEnumerable<string> TwoYieldedBaseUrls()
    {
        yield return "https://stub.host-one";
        yield return "https://stub.host-two";
    }

    private sealed class HttpMessageHandlerExceptionThenOk(Func<int, HttpResponseMessage> onAttempt)
        : HttpMessageHandler
    {
        private int _attempt;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _attempt);
            var response = onAttempt(n);
            return Task.FromResult(response);
        }
    }
}
