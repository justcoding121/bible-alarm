#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DownloadServiceTests
{
    private sealed class OkBytesHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        }
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class UrlRoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses;

        public UrlRoutingHandler(Dictionary<string, byte[]> responses) => _responses = responses;

        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            RequestedUrls.Add(url);

            if (_responses.TryGetValue(url, out var payload))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload),
                };
                response.Content.Headers.ContentLength = payload.Length;
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class HeadContentLengthHandler(long contentLength) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Head)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content.Headers.ContentLength = contentLength;
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
        }
    }

    [Fact]
    public async Task DownloadAsync_ReturnsResponseBody_OnSuccessfulGet()
    {
        var payload = new byte[] { 9, 8, 7 };
        using var sut = new DownloadService(new OkBytesHandler(payload), TestLogging.CreateLogger());

        var bytes = await sut.DownloadAsync("https://example.test/content.bin");

        Assert.Equal(payload, bytes);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndDoesNotThrow()
    {
        using var sut = new DownloadService(new OkBytesHandler([]), TestLogging.CreateLogger());

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public async Task DownloadAsync_does_not_retry_permanent_404()
    {
        var handler = new StatusCodeHandler(HttpStatusCode.NotFound);
        using var sut = new DownloadService(handler, TestLogging.CreateLogger());

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.DownloadAsync("https://example.test/missing.bin"));

        Assert.Equal(1, handler.SendCount);
    }

    [Fact]
    public async Task DownloadAsync_uses_alternative_url_when_primary_fails()
    {
        var primary = "https://example.test/primary.bin";
        var alternate = "https://example.test/alternate.bin";
        var payload = new byte[] { 1, 2, 3 };
        var handler = new UrlRoutingHandler(new Dictionary<string, byte[]>
        {
            [alternate] = payload,
        });
        using var sut = new DownloadService(handler, TestLogging.CreateLogger());

        var bytes = await sut.DownloadAsync(primary, alternate);

        Assert.Equal(payload, bytes);
        Assert.Equal(new[] { primary, alternate }, handler.RequestedUrls);
    }

    [Fact]
    public async Task GetContentLengthAsync_returns_length_from_head_response()
    {
        using var sut = new DownloadService(new HeadContentLengthHandler(12_345), TestLogging.CreateLogger());

        var length = await sut.GetContentLengthAsync("https://example.test/size.bin");

        Assert.Equal(12_345, length);
    }

    [Fact]
    public async Task FileExists_returns_true_when_head_succeeds()
    {
        using var sut = new DownloadService(new HeadContentLengthHandler(1), TestLogging.CreateLogger());

        var exists = await sut.FileExists("https://example.test/exists.bin");

        Assert.True(exists);
    }

    [Fact]
    public async Task DownloadWithProgressAsync_reports_progress_during_download()
    {
        var payload = new byte[16_384];
        using var sut = new DownloadService(new OkBytesHandler(payload), TestLogging.CreateLogger());
        var progressReports = new List<(long Read, long? Total)>();

        var bytes = await sut.DownloadWithProgressAsync(
            "https://example.test/large.bin",
            (read, total) => progressReports.Add((read, total)));

        Assert.Equal(payload, bytes);
        Assert.Contains(progressReports, p => p.Read == 0 && p.Total == payload.Length);
        Assert.Contains(progressReports, p => p.Read == payload.Length && p.Total == payload.Length);
    }
}
