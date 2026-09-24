#nullable enable

using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Tests.Support;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Bible.Alarm.Tests;

public sealed class RemoteMp4ArtworkExtractorTests
{
    private static readonly byte[] MinimalFtyp =
    [
        0, 0, 0, 20,
        (byte)'f', (byte)'t', (byte)'y', (byte)'p',
        (byte)'m', (byte)'p', (byte)'4', (byte)'2',
        0, 0, 0, 0,
        0, 0, 0, 0
    ];

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

    private sealed class ScriptedHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        public ScriptedHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            this.respond = respond;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public void Enqueue(HttpResponseMessage response) => responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responses.Count > 0 ? responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.NotFound));
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

    [Theory]
    [InlineData("https://cdn.example/video.mp4?token=abc", true)]
    [InlineData("https://cdn.example/VIDEO.MP4?x=1&y=2", true)]
    [InlineData("https://cdn.example/clip.Mp4?", true)]
    [InlineData("https://cdn.example/track.mp3?file=video.mp4", false)]
    [InlineData("https://cdn.example/video.mp4.bak?v=1", false)]
    [InlineData("https://cdn.example/noext?name=a.mp4", false)]
    public void IsMp4Url_respects_path_extension_and_ignores_query_string(string url, bool expected)
    {
        Assert.Equal(expected, RemoteMp4ArtworkExtractor.IsMp4Url(url));
    }

    [Fact]
    public void FindAndExtractMoov_returns_moov_atom_from_crafted_buffer()
    {
        var moovPayload = Encoding.ASCII.GetBytes("meta");
        var moov = BuildAtom("moov", moovPayload);
        var buffer = Concat(MinimalFtyp, moov);

        var extracted = RemoteMp4ArtworkExtractor.FindAndExtractMoov(buffer);

        Assert.NotNull(extracted);
        Assert.Equal(moov, extracted);
    }

    [Fact]
    public void FindAndExtractMoov_returns_null_when_buffer_has_no_moov()
    {
        var mdat = BuildAtom("mdat", new byte[16]);
        var buffer = Concat(MinimalFtyp, mdat);

        Assert.Null(RemoteMp4ArtworkExtractor.FindAndExtractMoov(buffer));
    }

    [Fact]
    public void FindAndExtractMoovFromTail_finds_moov_near_end()
    {
        var padding = new byte[32];
        var moov = BuildAtom("moov", Encoding.ASCII.GetBytes("tail"));
        var tail = Concat(padding, moov);

        var extracted = RemoteMp4ArtworkExtractor.FindAndExtractMoovFromTail(tail);

        Assert.NotNull(extracted);
        Assert.Equal(moov, extracted);
    }

    [Fact]
    public async Task GetContentLength_returns_null_when_head_fails_and_range_probe_fails()
    {
        using var handler = new ScriptedHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler, disposeHandler: false);

        var length = await RemoteMp4ArtworkExtractor.GetContentLengthAsync(
            client,
            "https://cdn.example/missing.mp4",
            CancellationToken.None);

        Assert.Null(length);
    }

    [Fact]
    public async Task GetContentLength_uses_range_probe_when_head_is_not_successful()
    {
        using var handler = new ScriptedHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([0])
            };
            msg.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, 8192);
            return msg;
        });
        using var client = new HttpClient(handler, disposeHandler: false);

        var length = await RemoteMp4ArtworkExtractor.GetContentLengthAsync(
            client,
            "https://cdn.example/video.mp4",
            CancellationToken.None);

        Assert.Equal(8192, length);
    }

    [Fact]
    public async Task GetContentLength_returns_null_when_content_range_is_malformed()
    {
        using var handler = new ScriptedHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([0])
            };
            msg.Headers.TryAddWithoutValidation("Content-Range", "bytes 0-0");
            return msg;
        });
        using var client = new HttpClient(handler, disposeHandler: false);

        Assert.Null(await RemoteMp4ArtworkExtractor.GetContentLengthAsync(
            client,
            "https://cdn.example/video.mp4",
            CancellationToken.None));
    }

    [Fact]
    public async Task GetContentLength_returns_null_when_content_range_total_is_star()
    {
        using var handler = new ScriptedHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([0])
            };
            msg.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0);
            return msg;
        });
        using var client = new HttpClient(handler, disposeHandler: false);

        Assert.Null(await RemoteMp4ArtworkExtractor.GetContentLengthAsync(
            client,
            "https://cdn.example/video.mp4",
            CancellationToken.None));
    }

    [Fact]
    public async Task GetContentLength_returns_null_when_content_range_total_is_below_minimum()
    {
        using var handler = new ScriptedHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([0])
            };
            msg.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, 16);
            return msg;
        });
        using var client = new HttpClient(handler, disposeHandler: false);

        Assert.Null(await RemoteMp4ArtworkExtractor.GetContentLengthAsync(
            client,
            "https://cdn.example/tiny.mp4",
            CancellationToken.None));
    }

    [Fact]
    public async Task GetContentLength_returns_null_when_range_probe_succeeds_without_content_range_header()
    {
        using var handler = new ScriptedHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([0])
            };
        });
        using var client = new HttpClient(handler, disposeHandler: false);

        Assert.Null(await RemoteMp4ArtworkExtractor.GetContentLengthAsync(
            client,
            "https://cdn.example/video.mp4",
            CancellationToken.None));
    }

    [Fact]
    public async Task FetchRangeAsync_returns_body_on_partial_content_success()
    {
        var expected = Encoding.ASCII.GetBytes("range-payload");
        using var handler = new ScriptedHttpHandler(request =>
        {
            Assert.NotNull(request.Headers.Range);
            var range = Assert.Single(request.Headers.Range!.Ranges);
            Assert.Equal(0, range.From);
            Assert.Equal(12, range.To);

            var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(expected)
            };
            msg.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 12, 100);
            return msg;
        });
        using var client = new HttpClient(handler, disposeHandler: false);

        var body = await RemoteMp4ArtworkExtractor.FetchRangeAsync(
            client,
            "https://cdn.example/video.mp4",
            from: 0,
            to: 12,
            CancellationToken.None);

        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task FetchRangeAsync_returns_null_when_range_request_fails()
    {
        using var handler = new ScriptedHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable));
        using var client = new HttpClient(handler, disposeHandler: false);

        Assert.Null(await RemoteMp4ArtworkExtractor.FetchRangeAsync(
            client,
            "https://cdn.example/video.mp4",
            from: 0,
            to: 99,
            CancellationToken.None));
    }

    [Fact]
    public async Task TryExtractMetadata_uses_range_head_chunk_when_moov_is_present()
    {
        var moov = BuildAtom("moov", Encoding.ASCII.GetBytes("x"));
        var headChunk = Concat(MinimalFtyp, moov);
        long contentLength = headChunk.Length + 1024;

        using var handler = new ScriptedHttpHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                var head = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                };
                head.Content.Headers.ContentLength = contentLength;
                return head;
            }

            if (request.Headers.Range?.Ranges.FirstOrDefault() is { } range
                && range.From == 0)
            {
                var msg = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(headChunk)
                };
                msg.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                    0,
                    headChunk.Length - 1,
                    contentLength);
                return msg;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());

        var meta = await sut.TryExtractMetadataAsync("https://cdn.example/media.mp4?tok=1");

        Assert.NotNull(meta);
        Assert.Null(meta.Title);
        Assert.Null(meta.Artist);
        Assert.Null(meta.Album);
        Assert.Null(meta.ArtworkBytes);
    }

    [Fact]
    public async Task TryExtractMetadata_returns_null_when_cancelled()
    {
        using var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        });
        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.Null(await sut.TryExtractMetadataAsync("https://cdn.example/media.mp4", cts.Token));
    }

    private static byte[] BuildAtom(string type, byte[] payload)
    {
        if (type.Length != 4)
        {
            throw new ArgumentException("ISO BMFF type must be 4 characters.", nameof(type));
        }

        var size = 8 + payload.Length;
        var atom = new byte[size];
        atom[0] = (byte)((size >> 24) & 0xFF);
        atom[1] = (byte)((size >> 16) & 0xFF);
        atom[2] = (byte)((size >> 8) & 0xFF);
        atom[3] = (byte)(size & 0xFF);
        Encoding.ASCII.GetBytes(type, 0, 4, atom, 4);
        Buffer.BlockCopy(payload, 0, atom, 8, payload.Length);
        return atom;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var length = parts.Sum(p => p.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
