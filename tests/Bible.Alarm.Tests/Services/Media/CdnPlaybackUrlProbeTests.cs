#nullable enable

using System.Net;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class CdnPlaybackUrlProbeTests
{
    private sealed class ScriptedHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public void RespondWith(params HttpResponseMessage[] items)
        {
            foreach (var r in items)
            {
                responses.Enqueue(r);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!responses.TryDequeue(out var r))
            {
                throw new InvalidOperationException($"No scripted response left for {request.Method} {request.RequestUri}");
            }

            return Task.FromResult(r);
        }
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated failure");
    }

    private static CdnPlaybackUrlProbe Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler, disposeHandler: true), TestLogging.CreateLogger());

    [Fact]
    public async Task White_space_or_non_http_scheme_returns_indeterminate()
    {
        var sut = Create(new ScriptedHttpHandler());

        Assert.Equal(CdnUrlProbeOutcome.Indeterminate, await sut.ProbeStreamingUrlAsync(""));
        Assert.Equal(CdnUrlProbeOutcome.Indeterminate, await sut.ProbeStreamingUrlAsync("   "));
        Assert.Equal(CdnUrlProbeOutcome.Indeterminate, await sut.ProbeStreamingUrlAsync("ftp://host/x.mp3"));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.PartialContent)]
    public async Task Head_success_stops_at_resource_reachable(HttpStatusCode success)
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(success));

        Assert.Equal(CdnUrlProbeOutcome.ResourceReachable, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Head_not_found_or_gone(HttpStatusCode code)
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(code));

        Assert.Equal(CdnUrlProbeOutcome.NotFoundOrGone, await Create(h).ProbeStreamingUrlAsync("http://cdn.example/track.m4a"));
    }

    [Theory]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task Head_405_or_501_retries_with_range_get(HttpStatusCode headCode)
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(headCode));
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.PartialContent));

        Assert.Equal(CdnUrlProbeOutcome.ResourceReachable, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task Head_405_range_get_success_2xx_is_reachable(HttpStatusCode getCode)
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
        h.RespondWith(new HttpResponseMessage(getCode));

        Assert.Equal(CdnUrlProbeOutcome.ResourceReachable, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Fact]
    public async Task Head_405_range_get_forbidden_returns_indeterminate()
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.Forbidden));

        Assert.Equal(CdnUrlProbeOutcome.Indeterminate, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Fact]
    public async Task Head_503_returns_indeterminate()
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        Assert.Equal(CdnUrlProbeOutcome.Indeterminate, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Fact]
    public async Task Head_other_client_error_returns_indeterminate()
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.Forbidden));

        Assert.Equal(CdnUrlProbeOutcome.Indeterminate, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Fact]
    public async Task Range_get_followup_not_found_returns_not_found_or_gone()
    {
        var h = new ScriptedHttpHandler();
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
        h.RespondWith(new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal(CdnUrlProbeOutcome.NotFoundOrGone, await Create(h).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Fact]
    public async Task Network_error_returns_indeterminate()
    {
        Assert.Equal(
            CdnUrlProbeOutcome.Indeterminate,
            await Create(new ThrowingHttpHandler()).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4"));
    }

    [Fact]
    public async Task Already_canceled_token_returns_indeterminate()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Equal(
            CdnUrlProbeOutcome.Indeterminate,
            await Create(new ScriptedHttpHandler()).ProbeStreamingUrlAsync("https://cdn.example/audio.mp4", cts.Token));
    }
}
