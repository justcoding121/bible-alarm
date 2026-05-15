#nullable enable

using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Tests.Support;
using System.Net;
using System.Linq;

namespace Bible.Alarm.Tests;

public sealed class RemoteMp4ArtworkExtractorTests
{
    private sealed class SequentialMp4HttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Head)
            {
                var head = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                };
                head.Content.Headers.ContentLength = 500;
                return Task.FromResult(head);
            }

            if (request.Headers.Range != null && request.Headers.Range.Ranges.Count > 0)
            {
                var r = request.Headers.Range.Ranges.First();
                if (r.From == 0 && r.To == 0)
                {
                    var bytes = new byte[] { 0 };
                    var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
                    {
                        Content = new ByteArrayContent(bytes)
                    };
                    msg.Headers.TryAddWithoutValidation("Content-Range", "bytes 0-0/500");
                    return Task.FromResult(msg);
                }
            }

            var empty = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[64])
            };
            return Task.FromResult(empty);
        }
    }

    [Fact]
    public async Task TryExtractMetadata_returns_null_for_non_mp4_url()
    {
        using var handler = new HttpClientHandler();
        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync("https://cdn.example/track.mp3"));
    }

    [Fact]
    public async Task TryExtractMetadata_returns_null_for_short_and_empty_urls()
    {
        using var handler = new HttpClientHandler();
        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync(""));
        Assert.Null(await sut.TryExtractMetadataAsync("x.mp"));
    }

    [Fact]
    public async Task TryExtractMetadata_exercises_http_pipeline_then_returns_null_when_moov_missing()
    {
        using var handler = new SequentialMp4HttpHandler();
        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync("https://cdn.example/media.mp4"));
    }
}
