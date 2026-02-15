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
            byte[] artworkData = [];
            long? contentLength = null;

            // HTTP or HTTPS URL
            if (uri is not null &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var contentLengthResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                contentLength = contentLengthResponse.Content.Headers.ContentLength ?? 0;

                var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
                stream = response.IsSuccessStatusCode ? await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false) : null;
            }
            // file:// URI or absolute filesystem path (e.g. app data path on Android)
            else if ((uri is not null && uri.Scheme == Uri.UriSchemeFile) || IsAbsoluteFilePath(url))
            {
                var normalizedFilePath = NormalizeFilePath(
                    uri is not null && uri.Scheme == Uri.UriSchemeFile ? uri.LocalPath : url);

                if (File.Exists(normalizedFilePath))
                {
                    stream = File.OpenRead(normalizedFilePath);
                    contentLength = await GetByteCountFromStream(stream, cancellationToken);
                }
            }
            // Relative File Path (asset)
            else if (Uri.TryCreate(url, UriKind.Relative, out _))
            {
                var normalizedFilePath = NormalizeFilePath(url);

                stream = Platform.AppContext.Assets?.Open(normalizedFilePath) ?? throw new InvalidOperationException("Assets cannot be null");
                contentLength = await GetByteCountFromStream(stream, cancellationToken);
            }

            if (stream is not null)
            {
                // contentLength is always set when stream is not null (set in the conditions above)
                artworkData = new byte[contentLength!.Value];
                using var memoryStream = new MemoryStream(artworkData);
                await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            }

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
            catch
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

        static string NormalizeFilePath(string filePath) => filePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        static bool IsAbsoluteFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = NormalizeFilePath(path);
            return Path.IsPathRooted(normalized) && File.Exists(normalized);
        }

        static async ValueTask<long> GetByteCountFromStream(Stream stream, CancellationToken token)
        {
            if (stream.CanSeek)
            {
                return stream.Length;
            }

            long countedStreamBytes = 0;

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, token)) > 0)
            {
                countedStreamBytes += bytesRead;
            }

            return countedStreamBytes;
        }
    }

    internal static void DisposeClient() => client.Dispose();
}

