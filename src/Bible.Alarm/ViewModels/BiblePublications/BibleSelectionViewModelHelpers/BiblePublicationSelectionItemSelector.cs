#nullable enable
using System.Linq;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
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
    public async Task<(string? SectionCode, int TrackNumber, string SectionName, string TrackTitle)> 
        GetSectionAndTrackForPublicationAsync(
            PublicationListViewItemModel publication,
            LanguageListViewItemModel language,
            IFetchProgress? progress = null)
    {
        Log.Debug("GetSectionAndTrackForPublicationAsync: Starting for publication={PublicationCode}, language={LanguageCode}",
            publication.Code, language.Code);

        // Show progress while fetching
        progress?.UpdateProgress(0.1);
        
        // Each list item carries whether it has a language FK (LanguageId != null) or not.
        // Use that to decide the DB query path (no extra DB probing here).
        var publicationWithoutLanguage = publication.IsPublicationWithoutLanguage;

        // First, try to get sections from the database
        // IMPORTANT: Prefer direct DB query to avoid triggering background "ensure all sections" work.
        // Section presence is enough to determine sectioned vs non-sectioned for cascade defaults.
        SortedDictionary<string, BiblePublicationSection> sections;
        if (publicationWithoutLanguage)
        {
            sections = biblePublicationSectionService != null
                ? await biblePublicationSectionService.GetSectionsByPublicationWithoutLanguageAsync(publication.Code, default)
                : await mediaService.GetSectionsForPublicationWithoutLanguage(publication.Code);
        }
        else
        {
            sections = biblePublicationSectionService != null
                ? await biblePublicationSectionService.GetSectionsByPublicationAsync(language.Code, publication.Code, default)
                : await mediaService.GetBiblePublicationSections(language.Code, publication.Code);
        }

        progress?.UpdateProgress(0.3);

        // If publication has sections, use sectioned flow
        if (sections != null && sections.Count > 0)
        {
            Log.Debug("GetSectionAndTrackForPublicationAsync: Found {SectionCount} sections, using sectioned flow",
                sections.Count);
            progress?.UpdateProgress(0.5);
            var sectionedResult = await sectionTrackResolver.GetFirstSectionAndTrackFromSectionsAsync(
                publicationWithoutLanguage ? null : language.Code,
                publication.Code,
                sections,
                progress);
            Log.Debug("GetSectionAndTrackForPublicationAsync: Sectioned result: sectionCode={SectionCode}, trackNumber={TrackNumber}, sectionName={SectionName}, trackTitle={TrackTitle}",
                sectionedResult.SectionCode, sectionedResult.TrackNumber, sectionedResult.SectionName, sectionedResult.TrackTitle);
            
            // Warn if names are empty but codes/numbers are valid
            if (!string.IsNullOrWhiteSpace(sectionedResult.SectionCode) && string.IsNullOrWhiteSpace(sectionedResult.SectionName))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: SectionName is empty for sectionCode={SectionCode}", 
                    sectionedResult.SectionCode);
            }
            if (sectionedResult.TrackNumber > 0 && string.IsNullOrWhiteSpace(sectionedResult.TrackTitle))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: TrackTitle is empty for trackNumber={TrackNumber}", 
                    sectionedResult.TrackNumber);
            }
            
            return sectionedResult;
        }

        // No sections found - this is a non-sectioned publication (drama/video)
        Log.Debug("GetSectionAndTrackForPublicationAsync: No sections found, using non-sectioned flow");
        progress?.UpdateProgress(0.5);
        var result = await sectionTrackResolver.GetFirstTrackForNonSectionedAsync(
            publicationWithoutLanguage ? null : language.Code,
            publication.Code,
            progress);
        Log.Debug("GetSectionAndTrackForPublicationAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            result.TrackNumber, result.TrackTitle);
        return result;
    }

    /// <summary>
    /// When user selects a language, cascade to get first publication, section, and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// Progress milestones: 50% after publication+section saved, 100% after tracks saved.
    /// </summary>
    public async Task<(string? PublicationCode, string? SectionCode, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPublicationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language, IFetchProgress? progress = null)
    {
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Starting for language={LanguageCode}", language.Code);

        progress?.UpdateProgress(0.0);

        // Step 1: Pick the first viable publication for this language/category and ensure ONLY that publication exists.
        // IMPORTANT: This is a cascade path; it must NOT "download all publications".
        var stateValue = state.Value;
        var categoryName = stateValue.CurrentSchedule?.BiblePublicationCategoryName;

        string? publicationCode = null;
        BiblePublication? publication = null;
        bool publicationWithoutLanguage = false;

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
            var normalizedLanguageCode = language.Code.ToUpperInvariant();

            var query = db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode);

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(pl => pl.Category != null && pl.Category.CategoryName == categoryName);
            }

            var publicationLanguages = await query.ToListAsync();
            publicationLanguages = publicationLanguages
                .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.PublicationCodeComparer)
                .ThenBy(pl => pl.Id)
                .ToList();

            foreach (var pl in publicationLanguages)
            {
                // For non-English languages, harvest ONLY this publication (first section + its tracks for sectioned publications).
                // Progress is reported by EnsurePublicationExistsAsync: 50% (pub+section saved), 100% (tracks saved)
                if (languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var ensured = await languageContentService.EnsurePublicationExistsAsync(
                            pl.PublicationCode,
                            language.Code,
                            default,
                            progress);
                        if (!ensured)
                        {
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex,
                            "GetPublicationSectionAndTrackForLanguageAsync: Failed to harvest publication={PublicationCode} for language={LanguageCode}, trying next",
                            pl.PublicationCode,
                            language.Code);
                        continue;
                    }
                }

                // Load the actual publication (with localized name) after harvest.
                BiblePublication? candidate;
                if (biblePublicationService != null)
                {
                    candidate = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(language.Code, pl.PublicationCode);
                }
                else
                {
                    candidate = await db.BiblePublications
                        .AsNoTracking()
                        .Include(bp => bp.Language)
                        .Where(bp => bp.PublicationCode == pl.PublicationCode &&
                                     bp.Language != null &&
                                     bp.Language.LanguageCode == normalizedLanguageCode)
                        .FirstOrDefaultAsync();
                }

                if (candidate == null)
                {
                    continue;
                }

                publication = candidate;
                publicationCode = candidate.PublicationCode;
                publicationWithoutLanguage = candidate.LanguageId == null;
                break;
            }

            // Fallback: if nothing exists with a language FK, pick a publication without language FK for this category.
            if (publication == null && !string.IsNullOrWhiteSpace(categoryName))
            {
                var pubWithoutLanguage = await db.BiblePublications
                    .AsNoTracking()
                    .Include(bp => bp.Category)
                    .Where(bp => bp.Category != null &&
                                 bp.Category.CategoryName == categoryName &&
                                 bp.LanguageId == null)
                    .OrderBy(bp => bp.Id)
                    .FirstOrDefaultAsync();

                if (pubWithoutLanguage != null)
                {
                    publication = pubWithoutLanguage;
                    publicationCode = pubWithoutLanguage.PublicationCode;
                    publicationWithoutLanguage = true;
                }
            }
        }

        if (string.IsNullOrEmpty(publicationCode) || publication == null)
        {
            Log.Warning(
                "GetPublicationSectionAndTrackForLanguageAsync: No publication found for language={LanguageCode}, category={CategoryName}",
                language.Code,
                categoryName ?? "(null)");
            return (null, null, 0, string.Empty, string.Empty, string.Empty);
        }

        var publicationName = publication.Name;

        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Selected publication code={PublicationCode}, name={PublicationName}, withoutLanguage={WithoutLanguage}",
            publicationCode, publicationName, publicationWithoutLanguage);

        // For publications without LanguageId, query without a language code
        // For publications with LanguageId, use the language code
        string? languageCodeForQuery = publicationWithoutLanguage ? null : language.Code;

        // Ensure publication is harvested before getting sections
        // For publications with LanguageId, EnsurePublicationExistsAsync should have been called above,
        // but we ensure it here as well to handle edge cases (no progress passed - just a safety check)
        if (!publicationWithoutLanguage && languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Ensuring publication {PublicationCode} exists for language {LanguageCode} before getting sections",
                publicationCode, language.Code);
            try
            {
                // Don't pass progress here - this is a safety check, not the main fetch
                await languageContentService.EnsurePublicationExistsAsync(publicationCode, language.Code, default, null);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "GetPublicationSectionAndTrackForLanguageAsync: Failed to ensure publication {PublicationCode} exists, continuing anyway",
                    publicationCode);
            }
        }

        // Try to get sections to determine if publication is sectioned or not
        // IMPORTANT: Query sections directly from database first to avoid triggering full harvesting
        // Only fetch sections if they don't exist yet
        SortedDictionary<string, BiblePublicationSection>? sections = null;
        if (publicationWithoutLanguage)
        {
            // Query sections for publications without language (LanguageId=null)
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Querying sections for publication without language={PublicationCode}", publicationCode);
            sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
        }
        else
        {
            // Query sections directly from database without triggering EnsureAllSectionsForPublicationAsync
            // This avoids fetching all sections when we only need to check if the publication is sectioned
            if (biblePublicationSectionService != null)
            {
                sections = await biblePublicationSectionService.GetSectionsByPublicationAsync(language.Code, publicationCode, default);
                
                // If no sections found and publication was just harvested, it might be a non-sectioned publication
                // Or the publication might not exist yet - in that case, EnsurePublicationExistsAsync above should have created it
                // For sectioned publications, EnsurePublicationExistsAsync fetches the FIRST section + tracks, so we should have at least one section now.
                if (sections == null || sections.Count == 0)
                {
                    Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found in database after ensuring publication exists, publication is likely non-sectioned");
                }
            }
            else
            {
                // Fallback to MediaService method (read-only when progress is null).
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: biblePublicationSectionService is null, using MediaService.GetBiblePublicationSections");
                sections = await mediaService.GetBiblePublicationSections(language.Code, publicationCode);
            }
        }

        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get first section and track (data already saved, just reading from DB)
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Found {SectionCount} sections, using sectioned flow", sections.Count);
            // For publications without language, pass null as language code
            // Don't pass progress here - data is already saved, this is just reading/resolving
            var (sectionCode, firstTrackNumber, sectionName, firstTrackTitle) =
                await sectionTrackResolver.GetFirstSectionAndTrackFromSectionsAsync(languageCodeForQuery, publicationCode, sections, null);

            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Sectioned result: sectionCode={SectionCode}, sectionName={SectionName}, trackNumber={TrackNumber}, trackTitle={TrackTitle}",
                sectionCode, sectionName, firstTrackNumber, firstTrackTitle);

            // Warn if names are empty but codes/numbers are valid
            if (!string.IsNullOrWhiteSpace(sectionCode) && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: SectionName is empty for sectionCode={SectionCode}", 
                    sectionCode);
            }
            if (firstTrackNumber > 0 && string.IsNullOrWhiteSpace(firstTrackTitle))
            {
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: TrackTitle is empty for trackNumber={TrackNumber}", 
                    firstTrackNumber);
            }

            return (publicationCode, sectionCode, firstTrackNumber, sectionName, publicationName, firstTrackTitle);
        }

        // Non-sectioned publication (drama/video) - get first track directly (data already saved, just reading from DB)
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found, using non-sectioned flow");
        // For publications without language, pass null/empty as language code
        // Don't pass progress here - data is already saved, this is just reading/resolving
        var (_, trackNumber, _, trackTitle) = await sectionTrackResolver.GetFirstTrackForNonSectionedAsync(languageCodeForQuery, publicationCode, null);
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            trackNumber, trackTitle);
        return (publicationCode, null, trackNumber, string.Empty, publicationName, trackTitle);
    }
}
