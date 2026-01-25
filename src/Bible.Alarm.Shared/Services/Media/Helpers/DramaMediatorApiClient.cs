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

    public async Task<(string? LocalizedPubName, HashSet<string> SectionCodes)> FetchCategoryAndSectionsAsync(
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        // Use Mediator API to get the category (case-sensitive: "Dramas" or "DramaticBibleReadings")
        // Note: normalizedPublicationCode is lowercase, but API requires exact case
        string categoryKey;
        if (normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase))
        {
            categoryKey = "Dramas";
        }
        else if (normalizedPublicationCode.Equals("dramaticbiblereadings", StringComparison.OrdinalIgnoreCase))
        {
            categoryKey = "DramaticBibleReadings";
        }
        else
        {
            logger.Warning("Unknown drama publication code: {PublicationCode}", normalizedPublicationCode);
            return (null, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var mediatorUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{normalizedLanguageCode}/{categoryKey}?detailed=1";
        
        var response = await httpClient.GetAsync(mediatorUrl, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            logger.Warning("Failed to fetch drama category {CategoryKey} for language {LanguageCode}: {StatusCode}",
                categoryKey, normalizedLanguageCode, response.StatusCode);
            return (null, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("category", out var categoryElement))
        {
            logger.Warning("Invalid response structure for drama category {CategoryKey}", categoryKey);
            return (null, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        // Extract localized publication name
        // Concatenate parent category name with category name (e.g., "Audio" + "Dramas" = "Audio Dramas")
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
        
        // Concatenate parent category name with category name
        if (!string.IsNullOrEmpty(parentCategoryName) && !string.IsNullOrEmpty(categoryName))
        {
            localizedPubName = $"{parentCategoryName} {categoryName}";
        }
        else if (!string.IsNullOrEmpty(categoryName))
        {
            localizedPubName = categoryName;
        }

        // Extract section codes from category.media array
        if (!categoryElement.TryGetProperty("media", out var mediaArray) || mediaArray.ValueKind != JsonValueKind.Array)
        {
            logger.Warning("No media items found in drama category {CategoryKey}", categoryKey);
            return (localizedPubName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var sectionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaItem in mediaArray.EnumerateArray())
        {
            // Extract section code from naturalKey: "pub-{sectionCode}_{lang}_{number}_AUDIO"
            if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
            {
                var naturalKey = naturalKeyElement.GetString() ?? "";
                if (naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = naturalKey.Split('_');
                    if (parts.Length > 0)
                    {
                        var sectionCode = parts[0].Substring(4); // Remove "pub-" prefix
                        if (!string.IsNullOrEmpty(sectionCode))
                        {
                            sectionCodes.Add(sectionCode);
                        }
                    }
                }
            }
        }

        return (localizedPubName, sectionCodes);
    }
}
