#nullable enable
using System.Text;
using System.Text.Json;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaUrlRefreshService(ILogger logger, IDownloadService downloadService) : IMediaUrlRefreshService, IDisposable
{
    private readonly ILogger logger = logger;
    private readonly IDownloadService downloadService = downloadService;
    private bool isDisposed;

    private static readonly string[] jwOrgUrls =
    [
        UrlHelper.JwOrgIndexServiceBaseUrl,
        AppConstants.ApiEndpoints.JwOrgAlternativeIndexServiceUrl
    ];

    public async Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata)
    {
        try
        {
            var lookUpPath = trackMetadata.LookUpPath;
            if (string.IsNullOrEmpty(lookUpPath))
            {
                return null;
            }

            var playType = trackMetadata.PlayType;
            if (playType == PlayType.Bible)
            {
                return await GetBibleChapterUrl(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.BookNumber,
                    trackMetadata.ChapterNumber,
                    lookUpPath);
            }
            else
            {
                return await GetMusicTrackUrl(
                    trackMetadata.LanguageCode,
                    lookUpPath);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to refresh URL using LookUpPath");
            return null;
        }
    }

    public async Task<string?> GetBibleChapterUrl(string languageCode, string pubCode, int bookNumber, int chapter,
        string lookUpPath)
    {
        try
        {
            var harvestLink1 = $"{jwOrgUrls[0]}{lookUpPath}";
            var harvestLink2 = $"{jwOrgUrls[1]}{lookUpPath}";
            var tes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Check if root is an object, not an array
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.Warning("Root element is not an object (type: {ValueKind}) for Bible chapter URL refresh", root.ValueKind);
                return null;
            }

            if (!root.TryGetProperty("files", out var files))
            {
                logger.Warning("'files' property not found in JSON response for Bible chapter URL refresh");
                return null;
            }

            if (!files.TryGetProperty(languageCode, out var languageFiles))
            {
                logger.Warning("Language '{LanguageCode}' not found in files for Bible chapter URL refresh", languageCode);
                return null;
            }

            if (!languageFiles.TryGetProperty("MP3", out var mp3Files))
            {
                logger.Warning("'MP3' property not found for language '{LanguageCode}' in Bible chapter URL refresh", languageCode);
                return null;
            }

            // Check if MP3 is an array
            if (mp3Files.ValueKind != JsonValueKind.Array)
            {
                logger.Warning("'MP3' property is not an array (type: {ValueKind}) for language '{LanguageCode}' in Bible chapter URL refresh", mp3Files.ValueKind, languageCode);
                return null;
            }

            var mp3Array = mp3Files.EnumerateArray().ToList();
            if (mp3Array.Count == 0)
            {
                logger.Warning("No MP3 files found for language '{LanguageCode}' in Bible chapter URL refresh", languageCode);
                return null;
            }

            var firstMp3 = mp3Array[0];
            if (!firstMp3.TryGetProperty("file", out var fileElement) ||
                !fileElement.TryGetProperty("url", out var urlElement))
            {
                logger.Warning("Missing 'file.url' property in MP3 file for Bible chapter URL refresh");
                return null;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
            {
                logger.Warning("URL is null or empty in MP3 file for Bible chapter URL refresh");
                return null;
            }

            return url;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception in GetBibleChapterUrl for language '{LanguageCode}', book {BookNumber}, chapter {Chapter}", languageCode, bookNumber, chapter);
            return null;
        }
    }

    public async Task<string?> GetMusicTrackUrl(string languageCode, string lookUpPath)
    {
        try
        {
            var harvestLink1 = $"{jwOrgUrls[0]}{lookUpPath}";
            var harvestLink2 = $"{jwOrgUrls[1]}{lookUpPath}";

            var tes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Check if root is an object, not an array
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.Warning("Root element is not an object (type: {ValueKind}) for music track URL refresh", root.ValueKind);
                return null;
            }

            var lc = languageCode ?? AppConstants.Media.DefaultLanguageCode;

            if (lc == AppConstants.Media.LanguageCodePatchFrom)
            {
                lc = AppConstants.Media.LanguageCodePatchTo;
            }

            if (!root.TryGetProperty("files", out var files))
            {
                logger.Warning("'files' property not found in JSON response for music track URL refresh");
                return null;
            }

            if (!files.TryGetProperty(lc, out var languageFiles))
            {
                logger.Warning("Language '{LanguageCode}' not found in files for music track URL refresh", lc);
                return null;
            }

            if (!languageFiles.TryGetProperty("MP3", out var mp3Files))
            {
                logger.Warning("'MP3' property not found for language '{LanguageCode}' in music track URL refresh", lc);
                return null;
            }

            // Check if MP3 is an array
            if (mp3Files.ValueKind != JsonValueKind.Array)
            {
                logger.Warning("'MP3' property is not an array (type: {ValueKind}) for language '{LanguageCode}' in music track URL refresh", mp3Files.ValueKind, lc);
                return null;
            }

            var mp3Array = mp3Files.EnumerateArray().ToList();
            if (mp3Array.Count == 0)
            {
                logger.Warning("No MP3 files found for language '{LanguageCode}' in music track URL refresh", lc);
                return null;
            }

            var firstMp3 = mp3Array[0];
            if (!firstMp3.TryGetProperty("file", out var fileElement) ||
                !fileElement.TryGetProperty("url", out var urlElement))
            {
                logger.Warning("Missing 'file.url' property in MP3 file for music track URL refresh");
                return null;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
            {
                logger.Warning("URL is null or empty in MP3 file for music track URL refresh");
                return null;
            }

            return url;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception in GetMusicTrackUrl for language '{LanguageCode}'", languageCode ?? "null");
            return null;
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

