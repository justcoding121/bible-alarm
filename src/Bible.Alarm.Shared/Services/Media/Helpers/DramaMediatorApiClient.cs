#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for interacting with the Mediator API to fetch drama categories and section codes.
/// </summary>
internal sealed class DramaMediatorApiClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;

    public DramaMediatorApiClient(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(string? LocalizedPubName, List<(string SectionCode, int TrackNumber)> MediaItems)> FetchCategoryAndSectionsAsync(
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        var categoryKey = Bible.Alarm.Shared.Helpers.JwSourceHelper.GetCanonicalDramaPublicationCode(normalizedPublicationCode);
        if (categoryKey == null)
        {
            logger.Warning("Unknown drama publication code: {PublicationCode}", normalizedPublicationCode);
            return (null, new List<(string SectionCode, int TrackNumber)>());
        }

        var pathAndQuery = $"/categories/{normalizedLanguageCode}/{categoryKey}?detailed=1";
        var baseUrls = AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls;
        var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, pathAndQuery, cancellationToken);
        if (jsonString == null)
        {
            logger.Warning("Failed to fetch drama category {CategoryKey} for language {LanguageCode}",
                categoryKey, normalizedLanguageCode);
            return (null, new List<(string SectionCode, int TrackNumber)>());
        }
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("category", out var categoryElement))
        {
            logger.Warning("Invalid response structure for drama category {CategoryKey}", categoryKey);
            return (null, new List<(string SectionCode, int TrackNumber)>());
        }

        // Extract localized publication name.
        // Only prepend parent category when the pub name equals the category name (e.g. "Dramas" under "Dramas")
        // to avoid confusion; otherwise use the category name as-is (e.g. "The Good News According to Jesus").
        string? localizedPubName = null;
        string? categoryName = null;
        string? parentCategoryName = null;

        if (categoryElement.TryGetProperty("name", out var nameElement))
        {
            var rawName = nameElement.GetString();
            categoryName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
        }

        if (categoryElement.TryGetProperty("parentCategory", out var parentCategoryElement) &&
            parentCategoryElement.TryGetProperty("name", out var parentNameElement))
        {
            var rawParentName = parentNameElement.GetString();
            parentCategoryName = rawParentName != null ? WebUtility.HtmlDecode(rawParentName).Replace('\u00A0', ' ') : null;
        }

        if (!string.IsNullOrEmpty(categoryName))
        {
            var pubNameEqualsCategoryName = string.Equals(categoryName, "Dramas", StringComparison.OrdinalIgnoreCase);
            if (pubNameEqualsCategoryName && !string.IsNullOrEmpty(parentCategoryName))
            {
                localizedPubName = $"{parentCategoryName} {categoryName}";
            }
            else
            {
                localizedPubName = categoryName;
            }
        }

        if (!categoryElement.TryGetProperty("media", out var mediaArray) || mediaArray.ValueKind != JsonValueKind.Array)
        {
            logger.Warning("No media items found in drama category {CategoryKey}", categoryKey);
            return (localizedPubName, new List<(string SectionCode, int TrackNumber)>());
        }

        var mediaItems = new List<(string SectionCode, int TrackNumber)>();
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            if (!mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
            {
                continue;
            }

            var naturalKey = naturalKeyElement.GetString() ?? "";
            if (!naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = naturalKey.Split('_');
            if (parts.Length < 3)
            {
                continue;
            }

            var sectionCode = parts[0].Substring(4);
            if (string.IsNullOrEmpty(sectionCode))
            {
                continue;
            }

            if (!int.TryParse(parts[2], out var trackNumber) || trackNumber < 1)
            {
                continue;
            }

            mediaItems.Add((sectionCode, trackNumber));
        }

        return (localizedPubName, mediaItems);
    }
}
