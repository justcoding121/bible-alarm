#nullable enable
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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
    /// Maps publication codes to Mediator API category keys (same as pub code for DramasGoodNews, VOD*, etc.).
    /// </summary>
    public async Task<string?> FetchVideoLocalizedNameFromMediatorAsync(
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken = default)
    {
        if (!MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(normalizedPublicationCode, out var categoryKey))
        {
            return null;
        }

        try
        {
            var pathAndQuery = JwMediatorVideoCategoryApiRelativePath.Compose(normalizedLanguageCode, categoryKey);
            var baseUrls = AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls;
            var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, pathAndQuery, cancellationToken);
            if (jsonString == null)
            {
                logger.Debug("Failed to fetch video category {CategoryKey} for language {LanguageCode}",
                    categoryKey, normalizedLanguageCode);
                return null;
            }
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(root, out var localizedName))
            {
                logger.Debug("Fetched localized name '{LocalizedName}' for video {PublicationCode} in language {LanguageCode} from Mediator API",
                    localizedName, normalizedPublicationCode, normalizedLanguageCode);
                return localizedName;
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
