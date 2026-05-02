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
}
