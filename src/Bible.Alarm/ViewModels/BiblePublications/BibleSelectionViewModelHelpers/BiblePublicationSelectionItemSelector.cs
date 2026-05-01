#nullable enable
using System.Linq;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles cascade selection logic for Bible publications.
/// Cascade order: Language → Publication → Section → Track
/// </summary>
public sealed class BiblePublicationSelectionItemSelector
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly BiblePublicationSelectionSectionTrackResolver sectionTrackResolver;

    // Using centralized sorting helper from Bible.Alarm.Shared.Helpers.PublicationSortHelper

    public BiblePublicationSelectionItemSelector(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IBiblePublicationService? biblePublicationService = null,
        IBiblePublicationSectionService? biblePublicationSectionService = null,
        ILanguageContentService? languageContentService = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.biblePublicationService = biblePublicationService;
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.languageContentService = languageContentService;
        this.scopeFactory = scopeFactory ?? ServiceProviderManager.GetService<IServiceScopeFactory>() 
            ?? throw new InvalidOperationException("IServiceScopeFactory is required but not available");
        sectionTrackResolver = new BiblePublicationSelectionSectionTrackResolver(
            mediaService,
            this.scopeFactory,
            biblePublicationService,
            languageContentService);
    }

    /// <summary>
    /// When user selects a publication, cascade to get first section and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(string? SectionCode, string TrackCode, string SectionName, string TrackTitle)>
        GetSectionAndTrackForPublicationAsync(
            PublicationListViewItemModel publication,
            LanguageListViewItemModel language,
            IFetchProgress? progress = null)
    {
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackStarting,
            publication.Code, language.Code);

        var publicationWithoutLanguage = publication.IsPublicationWithoutLanguage;
        var sections = await LoadPublicationSectionsAsync(language.Code, publication.Code, publicationWithoutLanguage);

        if (sections != null && sections.Count > 0)
            return await ResolveSectionedPublicationFlowAsync(language, publication.Code, publicationWithoutLanguage, sections, progress);

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackNoSectionsUsingNonSectionedFlow);
        progress?.UpdateProgress(0.5);
        var result = await sectionTrackResolver.GetFirstTrackForNonSectionedAsync(
            publicationWithoutLanguage ? null : language.Code,
            publication.Code,
            progress);
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackNonSectionedResult,
            result.TrackCode, result.TrackTitle);
        return result;
    }

    private async Task<SortedDictionary<string, BiblePublicationSection>> LoadPublicationSectionsAsync(
        string languageCode,
        string publicationCode,
        bool publicationWithoutLanguage)
    {
        if (publicationWithoutLanguage)
        {
            return biblePublicationSectionService != null
                ? await biblePublicationSectionService.GetSectionsByPublicationWithoutLanguageAsync(publicationCode, default)
                : await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
        }

        return biblePublicationSectionService != null
            ? await biblePublicationSectionService.GetSectionsByPublicationAsync(languageCode, publicationCode, default)
            : await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
    }

    private async Task<(string? SectionCode, string TrackCode, string SectionName, string TrackTitle)> ResolveSectionedPublicationFlowAsync(
        LanguageListViewItemModel language,
        string publicationCode,
        bool publicationWithoutLanguage,
        SortedDictionary<string, BiblePublicationSection> sections,
        IFetchProgress? progress)
    {
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackFoundSectionsSectionedFlow,
            sections.Count);

        var sectionedResult = await sectionTrackResolver.GetFirstSectionAndTrackFromSectionsAsync(
            publicationWithoutLanguage ? null : language.Code,
            publicationCode,
            sections,
            progress);

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackSectionedResult,
            sectionedResult.SectionCode, sectionedResult.TrackCode, sectionedResult.SectionName, sectionedResult.TrackTitle);

        LogSectionedResultNameWarnings(sectionedResult);
        return sectionedResult;
    }

    private static void LogSectionedResultNameWarnings((string? SectionCode, string TrackCode, string SectionName, string TrackTitle) sectionedResult)
    {
        if (!string.IsNullOrWhiteSpace(sectionedResult.SectionCode) && string.IsNullOrWhiteSpace(sectionedResult.SectionName))
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackSectionNameEmptyForSectionCode,
                sectionedResult.SectionCode);
        }

        if (!string.IsNullOrWhiteSpace(sectionedResult.TrackCode) && string.IsNullOrWhiteSpace(sectionedResult.TrackTitle))
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackTrackTitleEmptyForTrackCode,
                sectionedResult.TrackCode);
        }
    }

    /// <summary>
    /// When user selects a language, cascade to get first publication, section, and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// Progress milestones: 50% after publication+section saved, 100% after tracks saved.
    /// </summary>
    public async Task<(string? PublicationCode, string? SectionCode, string TrackCode, string SectionName, string PublicationName, string TrackTitle)>
        GetPublicationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language, IFetchProgress? progress = null, string? categoryNameOverride = null)
    {
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionTrackLangStarting, language.Code);

        // Progress will be set by EnsurePublicationExistsAsync only when a fetch actually happens.
        // Step 1: Pick the first viable publication for this language/category and ensure ONLY that publication exists.
        // IMPORTANT: This is a cascade path; it must NOT "download all publications".
        var stateValue = state.Value;
        // Use the override when provided (e.g. during category change, state hasn't been updated yet).
        var categoryName = categoryNameOverride ?? stateValue.CurrentSchedule?.BiblePublicationCategoryName;

        var (publication, publicationWithoutLanguage) =
            await PickPublicationForLanguageCategoryAsync(language, categoryName, progress);

        if (publication is null)
        {
            Log.Warning(
                AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.NoPublicationFoundForLanguageCategory,
                language.Code,
                categoryName ?? "(null)");
            return (null, null, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        var publicationCode = publication.PublicationCode;
        if (string.IsNullOrEmpty(publicationCode))
        {
            Log.Warning(
                AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.NoPublicationFoundForLanguageCategory,
                language.Code,
                categoryName ?? "(null)");
            return (null, null, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        var publicationName = publication.Name;

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.SelectedPublicationCodeNameWithoutLanguage,
            publicationCode, publicationName, publicationWithoutLanguage);

        // For publications without LanguageId, query without a language code
        // For publications with LanguageId, use the language code
        string? languageCodeForQuery = publicationWithoutLanguage ? null : language.Code;

        await EnsurePublicationCatalogedBeforeSectionsSafetyAsync(publicationWithoutLanguage, publicationCode, language);

        var sections = await LoadSectionsDictionaryForPublicationAsync(
            publicationWithoutLanguage,
            publicationCode,
            language);

        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get first section and track (data already saved, just reading from DB)
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationFoundSectionsSectionedFlow, sections.Count);
            // For publications without language, pass null as language code
            // Don't pass progress here - data is already saved, this is just reading/resolving
            var (sectionCode, firstTrackCode, sectionName, firstTrackTitle) =
                await sectionTrackResolver.GetFirstSectionAndTrackFromSectionsAsync(languageCodeForQuery, publicationCode, sections, null);

            Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionedResult,
                sectionCode, sectionName, firstTrackCode, firstTrackTitle);

            // Warn if names are empty but codes/numbers are valid
            if (!string.IsNullOrWhiteSpace(sectionCode) && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionNameEmptyForSectionCode, 
                    sectionCode);
            }
            if (!string.IsNullOrWhiteSpace(firstTrackCode) && string.IsNullOrWhiteSpace(firstTrackTitle))
            {
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationTrackTitleEmptyForTrackCode, 
                    firstTrackCode);
            }

            return (publicationCode, sectionCode, firstTrackCode, sectionName, publicationName, firstTrackTitle);
        }

        // Non-sectioned publication (drama/video) - get first track directly (data already saved, just reading from DB)
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationNoSectionsUsingNonSectionedFlow);
        // For publications without language, pass null/empty as language code
        // Don't pass progress here - data is already saved, this is just reading/resolving
        var (_, trackCode, _, trackTitle) = await sectionTrackResolver.GetFirstTrackForNonSectionedAsync(languageCodeForQuery, publicationCode, null);
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationNonSectionedResult,
            trackCode, trackTitle);
        return (publicationCode, null, trackCode, string.Empty, publicationName, trackTitle);
    }

    private async Task<(BiblePublication? Publication, bool PublicationWithoutLanguage)> PickPublicationForLanguageCategoryAsync(
        LanguageListViewItemModel language,
        string? categoryName,
        IFetchProgress? progress)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var normalizedLanguageCode = language.Code.ToUpperInvariant();

        var publicationLanguages =
            await LoadOrderedPublicationLanguagesForLanguageAsync(db, normalizedLanguageCode, categoryName);

        foreach (var (pl, publicationCodeForDb) in publicationLanguages.Select(pl =>
                     (pl, PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(pl.PublicationCode))))
        {
            var picked = await TryPickPublicationFromLanguageRowAsync(
                language,
                normalizedLanguageCode,
                pl,
                publicationCodeForDb,
                db,
                progress);
            if (picked.HasValue)
            {
                return picked.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var fallback = await TryPickPublicationWithoutLanguageForCategoryAsync(db, categoryName);
            if (fallback != null)
            {
                return (fallback, true);
            }
        }

        return (null, false);
    }

    private static async Task<List<PublicationLanguage>> LoadOrderedPublicationLanguagesForLanguageAsync(
        MediaDbContext db,
        string normalizedLanguageCode,
        string? categoryName)
    {
        var query = db.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode);

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            query = query.Where(pl => pl.Category != null && pl.Category.CategoryCode == categoryName);
        }

        var publicationLanguages = await query.ToListAsync();
        return publicationLanguages
            .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.GetPublicationCodeComparerForCategory(categoryName))
            .ThenBy(pl => pl.Id)
            .ToList();
    }

    private async Task<(BiblePublication Publication, bool PublicationWithoutLanguage)?> TryPickPublicationFromLanguageRowAsync(
        LanguageListViewItemModel language,
        string normalizedLanguageCode,
        PublicationLanguage pl,
        string publicationCodeForDb,
        MediaDbContext db,
        IFetchProgress? progress)
    {
        if (languageContentService != null && !language.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            progress?.UpdateProgress(0.0);
            try
            {
                var ensured = await languageContentService.EnsurePublicationExistsAsync(
                    pl.PublicationCode,
                    language.Code,
                    progress);
                if (!ensured)
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.IsNetworkFailure(ex))
                {
                    throw;
                }

                Log.Debug(ex,
                    AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.FailedToCatalogPublicationTryingNext,
                    pl.PublicationCode,
                    language.Code);
                return null;
            }
        }

        BiblePublication? candidate;
        if (biblePublicationService != null)
        {
            candidate = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(language.Code, publicationCodeForDb);
        }
        else
        {
            candidate = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Where(bp => bp.PublicationCode == publicationCodeForDb &&
                         bp.Language != null &&
                         bp.Language.LanguageCode == normalizedLanguageCode)
                .FirstOrDefaultAsync();
        }

        if (candidate == null)
        {
            return null;
        }

        return (candidate, candidate.LanguageId == null);
    }

    private static async Task<BiblePublication?> TryPickPublicationWithoutLanguageForCategoryAsync(
        MediaDbContext db,
        string categoryName)
    {
        var categoryComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(categoryName);
        var candidates = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .Where(bp =>
                bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == categoryName) &&
                bp.LanguageId == null)
            .ToListAsync();

        return candidates
            .OrderBy(bp => bp.PublicationCode, categoryComparer)
            .ThenBy(bp => bp.Id)
            .FirstOrDefault();
    }

    private async Task EnsurePublicationCatalogedBeforeSectionsSafetyAsync(
        bool publicationWithoutLanguage,
        string publicationCode,
        LanguageListViewItemModel language)
    {
        if (publicationWithoutLanguage ||
            languageContentService == null ||
            language.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.EnsuringPublicationExistsBeforeGettingSections,
            publicationCode, language.Code);
        try
        {
            await languageContentService.EnsurePublicationExistsAsync(publicationCode, language.Code);
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
            {
                throw;
            }

            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.FailedToEnsurePublicationExistsContinuingAnyway,
                publicationCode);
        }
    }

    private async Task<SortedDictionary<string, BiblePublicationSection>?> LoadSectionsDictionaryForPublicationAsync(
        bool publicationWithoutLanguage,
        string publicationCode,
        LanguageListViewItemModel language)
    {
        if (publicationWithoutLanguage)
        {
            Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.QueryingSectionsPublicationWithoutLanguage, publicationCode);
            return await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
        }

        if (biblePublicationSectionService != null)
        {
            var sections = await biblePublicationSectionService.GetSectionsByPublicationAsync(language.Code, publicationCode, default);
            if (sections == null || sections.Count == 0)
            {
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.NoSectionsInDbAfterEnsuringLikelyNonSectioned);
            }

            return sections;
        }

        Log.Warning(AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.BiblePublicationSectionServiceNullUsingMediaServiceSections);
        return await mediaService.GetBiblePublicationSections(language.Code, publicationCode);
    }
}
