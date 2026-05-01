#nullable enable

using System.Net.Http;
using CommunityToolkit.Maui.Services;

namespace CommunityToolkit.Maui.Views;

internal static class AndroidArtworkBytesFetcher
{
    static readonly HttpClient client = new();

    internal static async Task<byte[]> GetBytesFromMetadataArtworkUrl(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return [];
        }

        Stream? stream = null;
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        try
        {
            var opened = await TryOpenArtworkStreamAsync(url, uri, cancellationToken).ConfigureAwait(false);
            stream = opened.Stream;
            if (stream is null)
            {
                return [];
            }

            var artworkData = new byte[opened.ByteLength];
            using var memoryStream = new MemoryStream(artworkData);
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

            return artworkData;
        }
        catch (Exception e)
        {
#if DEBUG
            // Use Serilog directly since this is a static method
            try
            {
                Serilog.Log.Debug(e, "Unable to retrieve {MetadataArtworkUrl} for {Url}", nameof(MediaElement.MetadataArtworkUrl), url);
            }
            catch (Exception)
            {
                // Serilog may not be initialized in static context, ignore
            }
#else
            _ = e; // Suppress unused variable warning in Release builds
#endif
            return [];
        }
        finally
        {
            if (stream is not null)
            {
                stream.Close();
                await stream.DisposeAsync();
            }
        }
    }

    private static async Task<(Stream? Stream, long ByteLength)> TryOpenArtworkStreamAsync(string url, Uri? uri, CancellationToken cancellationToken)
    {
        if (uri is not null &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var contentLengthResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var contentLength = contentLengthResponse.Content.Headers.ContentLength ?? 0;

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            var stream = response.IsSuccessStatusCode ? await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false) : null;
            return (stream, contentLength);
        }

        if ((uri is not null && uri.Scheme == Uri.UriSchemeFile) || IsAbsoluteFilePathLocal(url))
        {
            var normalizedFilePath = NormalizeFilePathLocal(
                uri is not null && uri.Scheme == Uri.UriSchemeFile ? uri.LocalPath : url);

            if (!File.Exists(normalizedFilePath))
            {
                return (null, 0);
            }

            var fileStream = File.OpenRead(normalizedFilePath);
            var length = await GetByteCountFromStreamLocal(fileStream, cancellationToken).ConfigureAwait(false);
            return (fileStream, length);
        }

        if (Uri.TryCreate(url, UriKind.Relative, out _))
        {
            var normalizedFilePath = NormalizeFilePathLocal(url);

            var assetStream = Platform.AppContext.Assets?.Open(normalizedFilePath) ?? throw new InvalidOperationException("Assets cannot be null");
            var assetLength = await GetByteCountFromStreamLocal(assetStream, cancellationToken).ConfigureAwait(false);
            return (assetStream, assetLength);
        }

        return (null, 0);

        static string NormalizeFilePathLocal(string filePath) =>
            filePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        static bool IsAbsoluteFilePathLocal(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = NormalizeFilePathLocal(path);
            return Path.IsPathRooted(normalized) && File.Exists(normalized);
        }

        static async ValueTask<long> GetByteCountFromStreamLocal(Stream stream, CancellationToken token)
        {
            if (stream.CanSeek)
            {
                return stream.Length;
            }

            long countedStreamBytes = 0;

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                countedStreamBytes += bytesRead;
            }

            return countedStreamBytes;
        }
    }

    internal static void DisposeClient() => client.Dispose();
}

