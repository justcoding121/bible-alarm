#nullable enable

using System.Net.Http.Headers;
using Bible.Alarm.Services.Media.Models;
using Serilog;
using File = TagLib.File;
using ReadStyle = TagLib.ReadStyle;

namespace Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;

/// <summary>
/// Extracts metadata (including artwork) from remote MP4 URLs using HTTP Range requests.
/// Locates the moov atom (from the start or end of file) and parses it with TagLib.
/// </summary>
internal sealed class RemoteMp4ArtworkExtractor
{
    private readonly HttpMessageHandler httpHandler;
    private readonly ILogger logger;

    private const string UserAgent = "BibleAlarm/1.0 (compatible; iOS; MAUI)";
    private const int ConnectionTimeoutSeconds = 15;
    private const int HeadChunkSize = 4 * 1024 * 1024;
    private const int TailChunkSize = 2 * 1024 * 1024;
    private const int MaxMoovSize = 4 * 1024 * 1024;

    private static readonly byte[] MoovType = [(byte)'m', (byte)'o', (byte)'o', (byte)'v'];
    private static readonly byte[] MinimalFtyp = [0, 0, 0, 20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2', 0, 0, 0, 0, 0, 0, 0, 0];

    public RemoteMp4ArtworkExtractor(HttpMessageHandler httpHandler, ILogger logger)
    {
        this.httpHandler = httpHandler;
        this.logger = logger;
    }

    /// <summary>
    /// Attempts to extract metadata from a remote MP4 URL via Range requests.
    /// Returns null if the URL is not MP4, Range is unsupported, or parsing fails.
    /// </summary>
    public async Task<MetaData?> TryExtractMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsMp4Url(url))
        {
            return null;
        }

        try
        {
            return await ExtractMetadataInternalAsync(url, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Remote MP4 artwork extraction failed for URL: {Url}", url);
            return null;
        }
    }

    private static bool IsMp4Url(string url)
    {
        if (string.IsNullOrEmpty(url) || url.Length < 5)
        {
            return false;
        }

        var q = url.AsSpan().IndexOf('?');
        var path = q >= 0 ? url.AsSpan(0, q) : url.AsSpan();
        return path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<MetaData?> ExtractMetadataInternalAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(httpHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds)
        };

        var contentLength = await GetContentLengthAsync(client, url, cancellationToken);
        long? len = contentLength;

        byte[]? moovBytes = null;
        if (len.HasValue && len.Value >= 28)
        {
            moovBytes = await TryGetMoovFromHeadAsync(client, url, cancellationToken);
            if (moovBytes == null && len.Value > TailChunkSize)
            {
                moovBytes = await TryGetMoovFromTailAsync(client, url, len.Value, cancellationToken);
            }
            else if (moovBytes == null && len.Value <= TailChunkSize)
            {
                var full = await FetchRangeAsync(client, url, 0, len.Value - 1, cancellationToken);
                if (full != null)
                {
                    moovBytes = FindAndExtractMoov(full);
                }
            }
        }

        if (moovBytes == null)
        {
            moovBytes = await TryGetMoovFromHeadAsync(client, url, cancellationToken);
            if (moovBytes == null)
            {
                var tailSuffix = await FetchSuffixRangeAsync(client, url, TailChunkSize, cancellationToken);
                if (tailSuffix != null)
                {
                    moovBytes = FindAndExtractMoovFromTail(tailSuffix, tailSuffix.Length);
                }
            }
        }

        if (moovBytes == null || moovBytes.Length == 0)
        {
            return null;
        }

        return await ParseMoovWithTagLibAsync(moovBytes, url);
    }

    private async Task<long?> GetContentLengthAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
        headRequest.Headers.UserAgent.ParseAdd(UserAgent);

        using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
        {
            return headResponse.Content.Headers.ContentLength.Value;
        }

        var fromRange = await GetContentLengthFromRangeRequestAsync(client, url, cancellationToken);
        return fromRange;
    }

    private async Task<long?> GetContentLengthFromRangeRequestAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Range = new RangeHeaderValue(0, 0);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string? contentRange = null;
        if (response.Headers.TryGetValues("Content-Range", out var values))
        {
            foreach (var v in values)
            {
                contentRange = v;
                break;
            }
        }

        if (string.IsNullOrEmpty(contentRange))
        {
            return null;
        }

        var slash = contentRange.IndexOf('/');
        if (slash < 0 || slash == contentRange.Length - 1)
        {
            return null;
        }

        var totalStr = contentRange[(slash + 1)..].Trim();
        if (totalStr == "*")
        {
            return null;
        }

        if (!long.TryParse(totalStr, out var totalLength) || totalLength < 28)
        {
            return null;
        }

        return totalLength;
    }

    private async Task<byte[]?> TryGetMoovFromHeadAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var head = await FetchRangeAsync(client, url, 0, HeadChunkSize - 1, cancellationToken);
        if (head == null)
        {
            return null;
        }

        return FindAndExtractMoov(head);
    }

    private async Task<byte[]?> TryGetMoovFromTailAsync(HttpClient client, string url, long contentLength, CancellationToken cancellationToken)
    {
        long from = Math.Max(0, contentLength - TailChunkSize);
        var tail = await FetchRangeAsync(client, url, from, contentLength - 1, cancellationToken);
        if (tail == null)
        {
            return null;
        }

        return FindAndExtractMoovFromTail(tail, contentLength - from);
    }

    private static byte[]? FindAndExtractMoov(byte[] buffer)
    {
        long offset = 0;
        while (offset + 8 <= buffer.Length)
        {
            int size = ReadUInt32BigEndian(buffer, (int)offset);
            if (size < 8)
            {
                return null;
            }

            if (size == 1 && offset + 16 <= buffer.Length)
            {
                size = (int)ReadUInt64BigEndian(buffer, (int)offset + 8);
                if (size < 16)
                {
                    return null;
                }
            }

            if (MatchesType(buffer, (int)offset + 4, MoovType))
            {
                if (size > MaxMoovSize || offset + size > buffer.Length)
                {
                    return null;
                }

                var moov = new byte[size];
                Buffer.BlockCopy(buffer, (int)offset, moov, 0, size);
                return moov;
            }

            offset += size;
        }

        return null;
    }

    private static byte[]? FindAndExtractMoovFromTail(byte[] tail, long fileTailLength)
    {
        for (int i = tail.Length - 4; i >= 4; i--)
        {
            if (!MatchesType(tail, i, MoovType))
            {
                continue;
            }

            int start = i - 4;
            int size32 = ReadUInt32BigEndian(tail, start);
            long atomSize;
            if (size32 == 1)
            {
                if (i + 12 > tail.Length)
                {
                    continue;
                }

                atomSize = ReadUInt64BigEndian(tail, i + 4);
                if (atomSize < 16 || atomSize > MaxMoovSize)
                {
                    continue;
                }
            }
            else
            {
                if (size32 < 8 || size32 > MaxMoovSize)
                {
                    continue;
                }

                atomSize = size32;
            }

            if (start + atomSize > tail.Length)
            {
                continue;
            }

            var moov = new byte[atomSize];
            Buffer.BlockCopy(tail, start, moov, 0, (int)atomSize);
            return moov;
        }

        return null;
    }

    private static bool MatchesType(byte[] buffer, int index, byte[] type)
    {
        if (index + type.Length > buffer.Length)
        {
            return false;
        }

        for (int i = 0; i < type.Length; i++)
        {
            if (buffer[index + i] != type[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int ReadUInt32BigEndian(byte[] b, int i)
    {
        return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
    }

    private static long ReadUInt64BigEndian(byte[] b, int i)
    {
        return ((long)b[i] << 56) | ((long)b[i + 1] << 48) | ((long)b[i + 2] << 40) | ((long)b[i + 3] << 32)
               | ((long)b[i + 4] << 24) | ((long)b[i + 5] << 16) | ((long)b[i + 6] << 8) | b[i + 7];
    }

    private async Task<byte[]?> FetchRangeAsync(HttpClient client, string url, long from, long to, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.Range = new RangeHeaderValue(from, to);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        long requestedLength = to - from + 1;
        var contentLength = response.Content.Headers.ContentLength;

        if (response.StatusCode == System.Net.HttpStatusCode.OK && from == 0)
        {
            return await ReadFirstBytesFromStreamAsync(response, (int)requestedLength, cancellationToken);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.OK && contentLength.HasValue && contentLength.Value > requestedLength)
        {
            const int maxFullBodyBytes = 10 * 1024 * 1024;
            if (from == 0 && contentLength.Value > maxFullBodyBytes)
            {
                return await ReadFirstBytesFromStreamAsync(response, (int)requestedLength, cancellationToken);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            int take = (int)Math.Min(requestedLength, bytes.Length);
            var slice = new byte[take];
            if (from > 0)
            {
                Buffer.BlockCopy(bytes, bytes.Length - take, slice, 0, take);
            }
            else
            {
                Buffer.BlockCopy(bytes, 0, slice, 0, take);
            }

            return slice;
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.OK && body.Length > requestedLength)
        {
            int take = (int)Math.Min(requestedLength, body.Length);
            var slice = new byte[take];
            if (from > 0)
            {
                Buffer.BlockCopy(body, body.Length - take, slice, 0, take);
            }
            else
            {
                Buffer.BlockCopy(body, 0, slice, 0, take);
            }

            return slice;
        }

        return body;
    }

    private static async Task<byte[]?> ReadFirstBytesFromStreamAsync(HttpResponseMessage response, int count, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[count];
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead == 0)
        {
            return null;
        }

        if (totalRead < count)
        {
            var exact = new byte[totalRead];
            Buffer.BlockCopy(buffer, 0, exact, 0, totalRead);
            return exact;
        }

        return buffer;
    }

    private async Task<byte[]?> FetchSuffixRangeAsync(HttpClient client, string url, int suffixLength, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.TryAddWithoutValidation("Range", "bytes=-" + suffixLength);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.OK && bytes.Length > suffixLength)
        {
            var slice = new byte[suffixLength];
            Buffer.BlockCopy(bytes, bytes.Length - suffixLength, slice, 0, suffixLength);
            return slice;
        }

        return bytes;
    }

    private async Task<MetaData?> ParseMoovWithTagLibAsync(byte[] moovBytes, string url)
    {
        string? tempFilePath = null;
        try
        {
            tempFilePath = Path.Combine(Path.GetTempPath(), $"ba_mp4_{Guid.NewGuid():N}.mp4");
            using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(MinimalFtyp);
                await fs.WriteAsync(moovBytes);
            }

            File? tagFile = null;
            try
            {
                tagFile = File.Create(tempFilePath, "video/mp4", ReadStyle.None);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "RemoteMp4ArtworkExtractor: Failed to create TagLib file from temp path {TempPath}", tempFilePath);
                return null;
            }

            if (tagFile == null)
            {
                return null;
            }

            using (tagFile)
            {
                var tag = tagFile.Tag;
                var meta = new MetaData
                {
                    Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title : null,
                    Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer :
                             !string.IsNullOrEmpty(tag.FirstAlbumArtist) ? tag.FirstAlbumArtist : null,
                    Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album : null
                };

                if (tag.Pictures != null && tag.Pictures.Length > 0)
                {
                    TagLib.IPicture? largest = null;
                    int largestSize = 0;
                    foreach (var pic in tag.Pictures)
                    {
                        if (pic?.Data?.Data != null && pic.Data.Data.Length > largestSize)
                        {
                            largest = pic;
                            largestSize = pic.Data.Data.Length;
                        }
                    }

                    if (largest?.Data?.Data != null)
                    {
                        meta.ArtworkBytes = largest.Data.Data;
                    }
                }

                return meta;
            }
        }
        finally
        {
            if (tempFilePath != null)
            {
                try
                {
                    System.IO.File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }
    }
}
