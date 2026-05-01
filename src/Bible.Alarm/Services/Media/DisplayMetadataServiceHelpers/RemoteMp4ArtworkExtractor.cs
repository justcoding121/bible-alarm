#nullable enable

using System.Linq;
using System.Net.Http.Headers;
using Bible.Alarm.Shared.Constants;
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
            logger.Debug(ex, AppConstants.Logging.RemoteArtworkExtractorDiagnosticsLog.Mp4ArtworkExtractionFailedForUrl, url);
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
        return path.EndsWith(AppConstants.Media.MediaVideoFileExtension.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<MetaData?> ExtractMetadataInternalAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(httpHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds)
        };

        var contentLength = await GetContentLengthAsync(client, url, cancellationToken);
        var moovBytes = await ResolveMoovBytesAsync(client, url, contentLength, cancellationToken);

        if (moovBytes == null || moovBytes.Length == 0)
        {
            return null;
        }

        return await ParseMoovWithTagLibAsync(moovBytes);
    }

    private static async Task<byte[]?> ResolveMoovBytesAsync(
        HttpClient client,
        string url,
        long? len,
        CancellationToken cancellationToken)
    {
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
                moovBytes = full != null ? FindAndExtractMoov(full) : null;
            }
        }

        if (moovBytes != null)
        {
            return moovBytes;
        }

        moovBytes = await TryGetMoovFromHeadAsync(client, url, cancellationToken);
        if (moovBytes != null)
        {
            return moovBytes;
        }

        var tailSuffix = await FetchSuffixRangeAsync(client, url, TailChunkSize, cancellationToken);
        return tailSuffix != null ? FindAndExtractMoovFromTail(tailSuffix) : null;
    }

    private static async Task<long?> GetContentLengthAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
        headRequest.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);

        using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
        {
            return headResponse.Content.Headers.ContentLength.Value;
        }

        var fromRange = await GetContentLengthFromRangeRequestAsync(client, url, cancellationToken);
        return fromRange;
    }

    private static async Task<long?> GetContentLengthFromRangeRequestAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
        request.Headers.Range = new RangeHeaderValue(0, 0);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string? contentRange = null;
        if (response.Headers.TryGetValues("Content-Range", out var values))
        {
            contentRange = values.FirstOrDefault();
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
        if (string.Equals(totalStr, "*", StringComparison.Ordinal))
        {
            return null;
        }

        if (!long.TryParse(totalStr, out var totalLength) || totalLength < 28)
        {
            return null;
        }

        return totalLength;
    }

    private static async Task<byte[]?> TryGetMoovFromHeadAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var head = await FetchRangeAsync(client, url, 0, HeadChunkSize - 1, cancellationToken);
        if (head == null)
        {
            return null;
        }

        return FindAndExtractMoov(head);
    }

    private static async Task<byte[]?> TryGetMoovFromTailAsync(HttpClient client, string url, long contentLength, CancellationToken cancellationToken)
    {
        long from = Math.Max(0, contentLength - TailChunkSize);
        var tail = await FetchRangeAsync(client, url, from, contentLength - 1, cancellationToken);
        if (tail == null)
        {
            return null;
        }

        return FindAndExtractMoovFromTail(tail);
    }

    private static bool TryReadIsoBmffAtomSize(byte[] buffer, long offset, out int atomSize)
    {
        atomSize = 0;
        if (offset + 8 > buffer.Length)
        {
            return false;
        }

        var size32 = ReadUInt32BigEndian(buffer, (int)offset);
        if (size32 == 1)
        {
            if (offset + 16 > buffer.Length)
            {
                return false;
            }

            var sz64 = ReadUInt64BigEndian(buffer, (int)offset + 8);
            if (sz64 < 16)
            {
                return false;
            }

            atomSize = (int)sz64;
            return true;
        }

        if (size32 < 8)
        {
            return false;
        }

        atomSize = size32;
        return true;
    }

    private static byte[]? FindAndExtractMoov(byte[] buffer)
    {
        long offset = 0;
        while (offset + 8 <= buffer.Length)
        {
            if (!TryReadIsoBmffAtomSize(buffer, offset, out var size))
            {
                return null;
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

    private static byte[]? FindAndExtractMoovFromTail(byte[] tail)
    {
        for (int i = tail.Length - 4; i >= 4; i--)
        {
            if (!MatchesType(tail, i, MoovType))
            {
                continue;
            }

            int start = i - 4;
            if (!TryReadTailMoovAtomExtent(tail, start, i, out var atomSize))
            {
                continue;
            }

            var moov = new byte[atomSize];
            Buffer.BlockCopy(tail, start, moov, 0, atomSize);
            return moov;
        }

        return null;
    }

    private static bool TryReadTailMoovAtomExtent(byte[] tail, int start, int moovTypeIndex, out int atomSize)
    {
        atomSize = 0;
        var size32 = ReadUInt32BigEndian(tail, start);
        long atomSizeLong;
        if (size32 == 1)
        {
            if (moovTypeIndex + 12 > tail.Length)
            {
                return false;
            }

            atomSizeLong = ReadUInt64BigEndian(tail, moovTypeIndex + 4);
            if (atomSizeLong < 16 || atomSizeLong > MaxMoovSize)
            {
                return false;
            }
        }
        else
        {
            if (size32 < 8 || size32 > MaxMoovSize)
            {
                return false;
            }

            atomSizeLong = size32;
        }

        if (start + atomSizeLong > tail.Length)
        {
            return false;
        }

        atomSize = (int)atomSizeLong;
        return true;
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

    private static async Task<byte[]?> FetchRangeAsync(HttpClient client, string url, long from, long to, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));
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
            return SliceRequestedPortion(bytes, from, requestedLength);
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.OK && body.Length > requestedLength)
        {
            return SliceRequestedPortion(body, from, requestedLength);
        }

        return body;
    }

    private static byte[] SliceRequestedPortion(byte[] bytes, long from, long requestedLength)
    {
        var take = (int)Math.Min(requestedLength, bytes.Length);
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

    private static async Task<byte[]?> FetchSuffixRangeAsync(HttpClient client, string url, int suffixLength, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(AppConstants.Media.MediaHttpUserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(AppConstants.Media.HttpAcceptAny));
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

    private async Task<MetaData?> ParseMoovWithTagLibAsync(byte[] moovBytes)
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
                tagFile = File.Create(tempFilePath, TagLibMimeConstants.VideoMp4, ReadStyle.None);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, AppConstants.Logging.RemoteArtworkExtractorDiagnosticsLog.Mp4FailedToCreateTagLibFileFromTempPath, tempFilePath);
                return null;
            }

            if (tagFile == null)
            {
                return null;
            }

            using (tagFile)
            {
                return BuildMetaFromTag(tagFile.Tag);
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
                catch (Exception ex)
                {
                    logger.Debug(ex, AppConstants.Logging.RemoteArtworkExtractorDiagnosticsLog.Mp4FailedToDeleteTempFileFromTempPath, tempFilePath);
                }
            }
        }
    }

    private static MetaData BuildMetaFromTag(TagLib.Tag tag)
    {
        string? artistMeta = null;
        if (!string.IsNullOrEmpty(tag.FirstPerformer))
        {
            artistMeta = tag.FirstPerformer;
        }
        else if (!string.IsNullOrEmpty(tag.FirstAlbumArtist))
        {
            artistMeta = tag.FirstAlbumArtist;
        }

        string? titleMeta = string.IsNullOrEmpty(tag.Title) ? null : tag.Title;
        string? albumMeta = string.IsNullOrEmpty(tag.Album) ? null : tag.Album;

        var meta = new MetaData
        {
            Title = titleMeta,
            Artist = artistMeta,
            Album = albumMeta
        };

        AssignLargestPicture(tag, meta);
        return meta;
    }

    private static void AssignLargestPicture(TagLib.Tag tag, MetaData meta)
    {
        if (tag.Pictures == null || tag.Pictures.Length == 0)
        {
            return;
        }

        TagLib.IPicture? largest = null;
        var largestSize = 0;
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
}
