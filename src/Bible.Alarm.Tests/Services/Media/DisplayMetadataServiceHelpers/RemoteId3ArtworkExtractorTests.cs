#nullable enable

using System.Net;
using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class RemoteId3ArtworkExtractorTests
{
    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public void Enqueue(HttpResponseMessage response) => responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responses.Count > 0 ? responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task TryExtractMetadataAsync_ReturnsNull_WhenRangeRequestFails()
    {
        using var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = new RemoteId3ArtworkExtractor(handler, TestLogging.CreateLogger());

        var meta = await sut.TryExtractMetadataAsync("https://example.invalid/track.mp3");

        Assert.Null(meta);
    }

    [Fact]
    public async Task TryExtractMetadataAsync_ReturnsNull_WhenHeaderTooShort()
    {
        using var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([1, 2, 3]),
        });
        var sut = new RemoteId3ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync("https://example.invalid/a.mp3"));
    }

    [Fact]
    public async Task TryExtractMetadataAsync_ReturnsNull_WhenOkResponseLargerThanRangeWindow()
    {
        using var handler = new QueueHandler();
        var oversized = new byte[256];
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(oversized),
        });
        var sut = new RemoteId3ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync("https://example.invalid/b.mp3"));
    }

    [Fact]
    public async Task TryExtractMetadataAsync_ReturnsNull_WhenCancelledDuringFetch()
    {
        using var handler = new BlockingHandler();
        var sut = new RemoteId3ArtworkExtractor(handler, TestLogging.CreateLogger());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.Null(await sut.TryExtractMetadataAsync("https://example.invalid/c.mp3", cts.Token));
    }

    /// <summary>
    /// Never completes unless cancelled — drives the OperationCanceled branch in <see cref="RemoteId3ArtworkExtractor.TryExtractMetadataAsync"/>.
    /// </summary>
    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("SendAsync should have been canceled.");
        }
    }
}
