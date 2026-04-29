#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for interacting with the Mediator API to fetch mediator categories and section codes.
/// </summary>
internal sealed class MediatorApiClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;

    public MediatorApiClient(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches the mediator category and returns tracks with CDN URLs. Do not call GETPUBMEDIALINKS for mediator publications.
    /// </summary>
    public async Task<(string? LocalizedPubName, List<MediatorTrack> Tracks)> FetchCategoryAndTracksAsync(
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        var canonicalCode = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode);
        if (canonicalCode == null)
        {
            logger.Warning("Unknown mediator publication code: {PublicationCode}", normalizedPublicationCode);
            return (null, new List<MediatorTrack>());
        }

        var categoryKey = JwSourceHelper.GetMediatorCategoryKey(canonicalCode);
        var pathAndQuery = $"/categories/{normalizedLanguageCode}/{categoryKey}";
        var baseUrls = AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls;
        var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, pathAndQuery, cancellationToken);
        if (jsonString == null)
        {
            logger.Warning("Failed to fetch mediator category {CategoryKey} for language {LanguageCode}",
                categoryKey, normalizedLanguageCode);
            return (null, new List<MediatorTrack>());
        }
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("category", out var categoryElement))
        {
            logger.Warning("Invalid response structure for mediator category {CategoryKey}", categoryKey);
            return (null, new List<MediatorTrack>());
        }

        string? localizedPubName = null;
        string? categoryName = null;
        string? parentCategoryName = null;

        if (categoryElement.TryGetProperty("name", out var nameElement))
        {
            var rawName = nameElement.GetString();
            categoryName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
        }

        if (categoryElement.TryGetProperty("parentCategory", out var parentCategoryElement) &&
            parentCategoryElement.TryGetProperty("name", out var parentNameElement))
        {
            var rawParentName = parentNameElement.GetString();
            parentCategoryName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawParentName);
        }

        if (!string.IsNullOrEmpty(categoryName))
        {
            var pubNameEqualsCategoryName = string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryDramas, StringComparison.OrdinalIgnoreCase);
            if (pubNameEqualsCategoryName && !string.IsNullOrEmpty(parentCategoryName))
                localizedPubName = $"{parentCategoryName} {categoryName}";
            else
                localizedPubName = categoryName;
        }

        if (!categoryElement.TryGetProperty("media", out var mediaArray) || mediaArray.ValueKind != JsonValueKind.Array)
        {
            logger.Warning("No media items found in mediator category {CategoryKey}", categoryKey);
            return (localizedPubName, new List<MediatorTrack>());
        }

        var tracks = new List<MediatorTrack>();
        var index = 0;
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            if (mediaItem.TryGetProperty("primaryCategory", out var primaryCatElement))
            {
                var primaryCat = primaryCatElement.GetString();
                if (!PrimaryCategoryMatches(categoryKey, primaryCat))
                    continue;
            }

            if (!mediaItem.TryGetProperty("naturalKey", out var _))
                continue;

            if (!mediaItem.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
                continue;

            string? url = null;
            foreach (var file in filesElement.EnumerateArray())
            {
                if (file.TryGetProperty("progressiveDownloadURL", out var urlEl))
                {
                    url = urlEl.GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(url))
                continue;

            var title = MediaTrackTitleHelper.UnknownTitle;
            if (mediaItem.TryGetProperty("title", out var titleElement))
            {
                title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
            }

            index++;
            var trackCode = index.ToString();
            tracks.Add(new MediatorTrack(trackCode, title, url));
        }

        return (localizedPubName, tracks);
    }

    internal sealed record MediatorTrack(string TrackCode, string Title, string Url);

    /// <summary>
    /// Category keys that aggregate media from multiple primary categories (e.g. conventions, family, children).
    /// When fetching one of these, accept all media regardless of primaryCategory.
    /// </summary>
    private static readonly HashSet<string> AggregatorCategoryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "2014Convention", "2015Convention", "2016Convention", "2017Convention", "2018Convention",
        "2019Convention", "2020Convention", "2021Convention", "2022Convention", "2023Convention", "2024Convention", "2025Convention",
        "ChildrenMovies", "ChildrenSongs", "FamilyMovies", "FamilyWorship", "TeenMovies", "TeenSocialLife", "TeenGoals", "TeenSpiritualGrowth", "TeenWhatPeersSay",
        AppConstants.Media.BiblePublicationCodeVODMoviesAnimated,
        AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
        AppConstants.Media.BiblePublicationCodeVODMoviesModernDay,
        AppConstants.Media.BiblePublicationCodeVODMoviesExtras,
        AppConstants.Media.BiblePublicationCodeDramasGoodNews,
        AppConstants.Media.BiblePublicationCodeSeriesWhatPeersSay,
        AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures,
        AppConstants.Media.BiblePublicationCodeSeriesBJFLessons
    };

    /// <summary>
    /// Mediator may return media with a more specific primaryCategory (e.g. SeriesBibleBooks for BibleBooks).
    /// Aggregator categories (conventions, family, etc.) list media from many primary categories; accept all.
    /// </summary>
    private static bool PrimaryCategoryMatches(string categoryKey, string? primaryCat)
    {
        if (string.IsNullOrEmpty(primaryCat))
        {
            return true;
        }

        if (AggregatorCategoryKeys.Contains(categoryKey))
        {
            return true;
        }

        if (string.Equals(primaryCat, categoryKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(categoryKey, "BibleBooks", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(primaryCat, "SeriesBibleBooks", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
