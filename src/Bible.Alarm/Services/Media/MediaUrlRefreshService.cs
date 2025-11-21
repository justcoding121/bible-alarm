#nullable enable
using System.Text;
using System.Text.Json;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaUrlRefreshService : IMediaUrlRefreshService
{
    private readonly ILogger _logger;
    private readonly IDownloadService _downloadService;

    private static readonly string[] JwOrgUrls =
    [
        UrlHelper.JwOrgIndexServiceBaseUrl,
        AppConstants.ApiEndpoints.JwOrgAlternativeIndexServiceUrl
    ];

    public MediaUrlRefreshService(ILogger logger, IDownloadService downloadService)
    {
        _logger = logger;
        _downloadService = downloadService;
    }

    public async Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata)
    {
        try
        {
            var lookUpPath = trackMetadata.LookUpPath;
            if (string.IsNullOrEmpty(lookUpPath))
                return null;

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
            _logger.Error(ex, "Failed to refresh URL using LookUpPath");
            return null;
        }
    }

    public async Task<string?> GetBibleChapterUrl(string languageCode, string pubCode, int bookNumber, int chapter,
        string lookUpPath)
    {
        try
        {
            var harvestLink1 = $"{JwOrgUrls[0]}{lookUpPath}";
            var harvestLink2 = $"{JwOrgUrls[1]}{lookUpPath}";
            var tes = await _downloadService.DownloadAsync(harvestLink1, harvestLink2);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            return root.GetProperty("files").GetProperty(languageCode).GetProperty("MP3")[0].GetProperty("file").GetProperty("url").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetMusicTrackUrl(string languageCode, string lookUpPath)
    {
        try
        {
            var harvestLink1 = $"{JwOrgUrls[0]}{lookUpPath}";
            var harvestLink2 = $"{JwOrgUrls[1]}{lookUpPath}";

            var tes = await _downloadService.DownloadAsync(harvestLink1, harvestLink2);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var lc = languageCode ?? AppConstants.Media.DefaultLanguageCode;

            if (lc == AppConstants.Media.LanguageCodePatchFrom) lc = AppConstants.Media.LanguageCodePatchTo;

            return root.GetProperty("files").GetProperty(lc).GetProperty("MP3")[0].GetProperty("file").GetProperty("url").GetString();
        }
        catch
        {
            return null;
        }
    }
}

