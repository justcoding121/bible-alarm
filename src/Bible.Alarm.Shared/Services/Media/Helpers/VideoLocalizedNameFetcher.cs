#nullable enable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for fetching localized publication names for videos from Mediator API.
/// </summary>
internal sealed class VideoLocalizedNameFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;

    public VideoLocalizedNameFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches localized publication name for videos from Mediator API.
    /// Maps publication codes to Mediator API category keys (e.g., "gnj" -> "DramasGoodNews").
    /// </summary>
    public async Task<string?> FetchVideoLocalizedNameFromMediatorAsync(
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken = default)
    {
        // Map video publication codes to Mediator API category keys
        string? categoryKey = normalizedPublicationCode.ToLowerInvariant() switch
        {
            "gnj" => "DramasGoodNews",
            "vodmoviesbibletimes" => "VODMoviesBibleTimes",
            "vodmoviesmodernday" => "VODMoviesModernDay",
            "vodmoviesanimated" => "VODMoviesAnimated",
            "vodmoviesextras" => "VODMoviesExtras",
            "vodlffvideosad" => "VODLFFVideosAD",
            "seriesdigfortreasures" => "SeriesDigForTreasures",
            "seriesbjflessons" => "SeriesBJFLessons",
            _ => null
        };

        if (categoryKey == null)
        {
            return null;
        }

        try
        {
            var categoryUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{normalizedLanguageCode}/{categoryKey}?detailed=1";
            var response = await httpClient.GetAsync(categoryUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                logger.Debug("Failed to fetch video category {CategoryKey} for language {LanguageCode}: {StatusCode}",
                    categoryKey, normalizedLanguageCode, response.StatusCode);
                return null;
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("category", out var category) &&
                category.TryGetProperty("name", out var nameElement))
            {
                var rawName = nameElement.GetString();
                var localizedName = rawName != null ? System.Net.WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                
                if (!string.IsNullOrEmpty(localizedName))
                {
                    logger.Debug("Fetched localized name '{LocalizedName}' for video {PublicationCode} in language {LanguageCode} from Mediator API",
                        localizedName, normalizedPublicationCode, normalizedLanguageCode);
                    return localizedName;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch localized name for video {PublicationCode} in language {LanguageCode} from Mediator API",
                normalizedPublicationCode, normalizedLanguageCode);
        }

        return null;
    }
}
