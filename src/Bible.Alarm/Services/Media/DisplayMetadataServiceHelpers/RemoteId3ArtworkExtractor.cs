#nullable enable

using System.Net.Http.Headers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Media.Models;
using Serilog;
using File = TagLib.File;
using ReadStyle = TagLib.ReadStyle;

namespace Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;

/// <summary>
/// Extracts ID3v2 metadata (including artwork) from remote MP3 URLs using HTTP Range requests.
/// Uses a two-step approach:
///   1. Fetch the first 10 bytes to read the ID3v2 header and parse the tag size.
///   2. Fetch the complete ID3v2 tag block and parse with TagLib from a temp file.
/// This avoids downloading the entire audio file just to get artwork.
/// </summary>
internal sealed class RemoteId3ArtworkExtractor
{
    private readonly HttpMessageHandler httpHandler;
    private readonly ILogger logger;

    // User-Agent string consistent with DownloadService.
    private const string UserAgent = "BibleAlarm/1.0 (compatible; iOS; MAUI)";
    private const int ConnectionTimeoutSeconds = 15;

    // ID3v2 header is exactly 10 bytes: "ID3" (3) + version (2) + flags (1) + size (4).
    private const int Id3v2HeaderSize = 10;

    // Safety cap: do not download more than 2 MB of tag data.
    private const int MaxTagBytes = 2 * 1024 * 1024;

    public RemoteId3ArtworkExtractor(HttpMessageHandler httpHandler, ILogger logger)
    {
        this.httpHandler = httpHandler;
        this.logger = logger;
    }

    /// <summary>
    /// Attempts to extract metadata (title, artist, artwork) from a remote HTTPS URL
    /// by reading only the ID3v2 tag block via Range requests.
    /// Returns null if the URL has no ID3v2 tag or the server doesn't support Range requests.
    /// </summary>
    public async Task<MetaData?> TryExtractMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
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
            logger.Debug(ex, "Remote ID3 artwork extraction failed for URL: {Url}", url);
            return null;
        }
    }

    private async Task<MetaData?> ExtractMetadataInternalAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(httpHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds)
        };

        // Step 1: Fetch the ID3v2 header (10 bytes) to determine tag size.
        var headerBytes = await FetchRangeAsync(client, url, 0, Id3v2HeaderSize - 1, cancellationToken);
        if (headerBytes == null || headerBytes.Length < Id3v2HeaderSize)
        {
            return null;
        }

        // Verify "ID3" magic.
        if (headerBytes[0] != (byte)'I' || headerBytes[1] != (byte)'D' || headerBytes[2] != (byte)'3')
        {
            return null;
        }

        // Parse syncsafe integer (bytes 6-9) to get tag body size.
        int tagBodySize = (headerBytes[6] << 21) | (headerBytes[7] << 14) | (headerBytes[8] << 7) | headerBytes[9];
        int totalTagSize = tagBodySize + Id3v2HeaderSize;

        if (tagBodySize <= 0 || totalTagSize > MaxTagBytes)
        {
            return null;
        }

        // Step 2: Fetch the complete ID3v2 tag block.
        var tagBytes = await FetchRangeAsync(client, url, 0, totalTagSize - 1, cancellationToken);
        if (tagBytes == null || tagBytes.Length < totalTagSize)
        {
            return null;
        }

        // Step 3: Write to a temp file and parse with TagLib.
        return await ParseTagBytesWithTagLibAsync(tagBytes);
    }

    private static async Task<byte[]?> FetchRangeAsync(HttpClient client, string url, long from, long to, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.Range = new RangeHeaderValue(from, to);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        // Accept both 206 Partial Content (proper Range support) and 200 OK (server ignored Range).
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        // If server returned 200 (full file) instead of 206, abort to avoid downloading full file.
        if (response.StatusCode == System.Net.HttpStatusCode.OK && bytes.Length > (to - from + 1))
        {
            return null;
        }

        return bytes;
    }

    private async Task<MetaData?> ParseTagBytesWithTagLibAsync(byte[] tagBytes)
    {
        string? tempFilePath = null;
        try
        {
            tempFilePath = Path.Combine(Path.GetTempPath(), $"ba_id3_{Guid.NewGuid():N}.tmp");
            await System.IO.File.WriteAllBytesAsync(tempFilePath, tagBytes);

            File? tagFile = null;
            try
            {
                // Try default detection first.
                tagFile = File.Create(tempFilePath, TagLibMimeConstants.AudioMpeg, ReadStyle.None);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "RemoteId3ArtworkExtractor: Failed to create TagLib file from temp path {TempPath}", tempFilePath);
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

                // Extract artwork from the tag.
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
            // Clean up temp file.
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
