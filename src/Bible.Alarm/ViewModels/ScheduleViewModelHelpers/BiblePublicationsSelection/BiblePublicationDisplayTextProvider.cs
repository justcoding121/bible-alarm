#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;

/// <summary>
/// Provides display text for bible-related properties.
/// Separated from BibleSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class BiblePublicationDisplayTextProvider
{
    private readonly IState<ApplicationState> state;
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly ICategoryNameService categoryNameService;
    private readonly IServiceScopeFactory serviceScopeFactory;

    private string? cachedPubSelectableKey;
    private bool cachedPubSelectableValue;
    private string? cachedSectionSelectableKey;
    private bool cachedSectionSelectableValue;

    public BiblePublicationDisplayTextProvider(IState<ApplicationState> state, ILogger logger, IMediaService mediaService, ICategoryNameService categoryNameService, IServiceScopeFactory serviceScopeFactory)
    {
        this.state = state;
        this.logger = logger;
        this.mediaService = mediaService;
        this.categoryNameService = categoryNameService;
        this.serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Determines if the section selection row should be visible.
    /// Returns false for dramas which don't have section selection.
    /// </summary>
    public bool GetIsSectionVisible()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            // Default to visible for traditional Bible reading
            return true;
        }

        return PublicationTypeHelper.HasSectionStructure(currentSchedule.BiblePublicationCode);
    }

    /// <summary>
    /// Gets the language direction string ("ltr" or "rtl").
    /// </summary>
    public string GetLanguageDirection()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        return currentSchedule?.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
    }

    /// <summary>
    /// Gets the FlowDirection based on the selected language direction.
    /// Returns RightToLeft for RTL languages, LeftToRight otherwise.
    /// </summary>
    public FlowDirection GetFlowDirection()
    {
        var direction = GetLanguageDirection();
        return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    public string GetLanguageDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        if (currentSchedule == null)
        {
            return string.Empty;
        }

        // Prefer display name (populated during bootstrap/effects), otherwise fall back to the language code.
        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageName))
        {
            logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - Returning '{LanguageName}' (LanguageCode: {LanguageCode})",
                currentSchedule.BiblePublicationLanguageName, currentSchedule.BiblePublicationLanguageCode ?? "null");
            return currentSchedule.BiblePublicationLanguageName;
        }

        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode))
        {
            return currentSchedule.BiblePublicationLanguageCode!;
        }

        // When LanguageCode is null (non-language publications like "iam"), default to English for display
        logger.Debug("BibleSelectionContainerViewModel: LanguageDisplayText getter - LanguageCode is null, defaulting to '{DefaultLanguage}' (English)",
            AppConstants.Media.DefaultLanguageCode);
        return AppConstants.Media.DefaultLanguageCode;
    }

    public string GetPublicationDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        if (currentSchedule == null)
        {
            return string.Empty;
        }

        // Prefer display name (populated during bootstrap/effects), otherwise fall back to the publication code.
        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationName))
        {
            return currentSchedule.BiblePublicationName;
        }

        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return currentSchedule.BiblePublicationCode!;
        }

        return string.Empty;
    }

    public string GetSectionDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        if (currentSchedule == null)
        {
            return string.Empty;
        }

        // Prefer display name (populated during bootstrap/effects), otherwise fall back to the section code.
        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationSectionName))
        {
            return currentSchedule.BiblePublicationSectionName;
        }

        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationSectionCode))
        {
            return currentSchedule.BiblePublicationSectionCode!;
        }

        return string.Empty;
    }

    public string GetTrackDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        if (currentSchedule == null)
        {
            return string.Empty;
        }

        // Prefer track title (populated during bootstrap/selection), otherwise fall back to track number.
        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationTrackTitle))
        {
            return currentSchedule.BiblePublicationTrackTitle;
        }

        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationTrackCode))
        {
            var pubCode = currentSchedule.BiblePublicationCode ?? string.Empty;

            // IMPORTANT:
            // PublicationTypeHelper.HasSectionStructure(pubCode) is true for both Bible (book+chapter)
            // and some non-Bible sectioned catalogs (e.g., music like "iam").
            var categoryName =
                currentSchedule.BiblePublicationCategoryName
                ?? JwSourceHelper.GetCategoryName(pubCode)
                ?? string.Empty;

            var isBible = string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase);
            var isMusicPublication = currentSchedule.BiblePublicationIsMusic;

            if (isBible && PublicationTypeHelper.HasSectionStructure(pubCode))
            {
                return $"Chapter {currentSchedule.BiblePublicationTrackCode}";
            }

            if (isMusicPublication && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationName))
            {
                return $"{currentSchedule.BiblePublicationName} {currentSchedule.BiblePublicationTrackCode}";
            }

            return $"Track {currentSchedule.BiblePublicationTrackCode}";
        }

        return string.Empty;
    }

    public string GetCategoryDisplayText()
    {
        var currentSchedule = state.Value.CurrentSchedule;

        if (currentSchedule == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            var displayName = categoryNameService.GetName(currentSchedule.BiblePublicationCategoryName, AppConstants.Media.DefaultLanguageCode);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
            return currentSchedule.BiblePublicationCategoryName;
        }

        // Fall back to deriving category from publication code (helps when display names haven't been hydrated yet).
        if (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            var categoryName = JwSourceHelper.GetCategoryName(currentSchedule.BiblePublicationCode);
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                return categoryName;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Determines if the language row should be visible.
    /// Language row is always visible. When LanguageCode is null (non-language publications like "iam"),
    /// it defaults to "E" (English) for display purposes.
    /// </summary>
    public static bool GetIsLanguageVisible() => true;

    /// <summary>
    /// Checks if there are multiple languages available for the current category.
    /// Returns true if there are 2 or more languages, false if only 1 or 0.
    /// </summary>
    public async Task<bool> GetIsLanguageSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        var categoryName = currentSchedule.BiblePublicationCategoryName;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            // No category selected, can't determine
            return false;
        }

        // If the row itself is hidden (e.g., selected publication has no language), it isn't selectable.
        if (!GetIsLanguageVisible())
        {
            return false;
        }

        // Avoid DB calls just to decide if the row is tappable.
        // In practice, Bible/Dramas/Music categories have multiple choices when language selection is applicable.
        if (string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(categoryName, "Dramas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var languages = await mediaService.GetBiblePublicationLanguages(categoryName);
            return languages.Count > 1;
        }
        catch
        {
            // On error, default to not selectable
            return false;
        }
    }

    /// <summary>
    /// Checks if there are multiple publications available for the current language and category.
    /// Queries the discovery table directly with caching to avoid stale Fluxor state.
    /// </summary>
    public async Task<bool> GetIsPublicationSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        var categoryName = currentSchedule.BiblePublicationCategoryName;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        var languageCode = currentSchedule.BiblePublicationLanguageCode;
        var cacheKey = $"{categoryName}|{languageCode ?? string.Empty}";

        if (string.Equals(cacheKey, cachedPubSelectableKey, StringComparison.Ordinal))
        {
            return cachedPubSelectableValue;
        }

        try
        {
            var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var query = db.PublicationLanguages
                .AsNoTracking()
                .Where(pl => pl.Category != null && pl.Category.CategoryCode == categoryName);

            if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
            {
                query = query.Where(pl =>
                    (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
                    pl.LanguageId == null);
            }
            else
            {
                query = query.Where(pl => pl.LanguageId == null);
            }

            var publicationCodes = await query
                .Select(pl => pl.PublicationCode)
                .ToListAsync();

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in publicationCodes)
            {
                var lower = code.ToLowerInvariant();
                if (PublicationTypeHelper.IsDrama(lower))
                {
                    unique.Add(lower.Equals("dramas", StringComparison.OrdinalIgnoreCase) ? "Dramas" : "DramaticBibleReadings");
                }
                else
                {
                    unique.Add(code);
                }
            }

            var result = unique.Count > 1;
            cachedPubSelectableKey = cacheKey;
            cachedPubSelectableValue = result;
            return result;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to query publication count for selectability. Category={Category}, Language={Language}", categoryName, languageCode);
            return false;
        }
    }

    /// <summary>
    /// Checks if there are multiple sections available for the current publication.
    /// Queries the discovery table directly with caching to avoid stale Fluxor state.
    /// </summary>
    public async Task<bool> GetIsSectionSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return false;
        }

        if (!GetIsSectionVisible())
        {
            return false;
        }

        var publicationCode = currentSchedule.BiblePublicationCode;
        var languageCode = currentSchedule.BiblePublicationLanguageCode;
        var cacheKey = $"{publicationCode}|{languageCode ?? string.Empty}";

        if (string.Equals(cacheKey, cachedSectionSelectableKey, StringComparison.Ordinal))
        {
            return cachedSectionSelectableValue;
        }

        try
        {
            var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var query = db.SectionLanguages
                .AsNoTracking()
                .Where(sl => sl.PublicationCode == publicationCode);

            if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
            {
                query = query.Where(sl =>
                    (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
                    sl.LanguageId == null);
            }
            else
            {
                query = query.Where(sl => sl.LanguageId == null);
            }

            var count = await query
                .Select(sl => sl.SectionCode)
                .Distinct()
                .CountAsync();

            var result = count > 1;
            cachedSectionSelectableKey = cacheKey;
            cachedSectionSelectableValue = result;
            return result;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to query section count for selectability. Publication={Publication}, Language={Language}", publicationCode, languageCode);
            return false;
        }
    }

    /// <summary>
    /// Checks if there are multiple tracks available for the current section/publication.
    /// Returns true if there are 2 or more tracks, false if only 1 or 0.
    /// Uses discovery-based expected count from state (set on cascade + populator) to avoid per-row DB queries.
    /// </summary>
    public Task<bool> GetIsTrackSelectableAsync()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null || string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode))
        {
            return Task.FromResult(false);
        }

        var expectedCount = currentSchedule.BiblePublicationTrackModalItemCount;
        return Task.FromResult(expectedCount.HasValue && expectedCount.Value > 1);
    }
}

