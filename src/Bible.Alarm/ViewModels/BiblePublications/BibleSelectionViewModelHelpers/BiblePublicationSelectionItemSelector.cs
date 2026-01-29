#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
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
    private readonly BiblePublicationSelectionPublicationChooser publicationChooser;

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
        publicationChooser = new BiblePublicationSelectionPublicationChooser(
            biblePublicationService,
            languageContentService,
            sectionTrackResolver);
    }

    /// <summary>
    /// When user selects a publication, cascade to get first section and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> 
        GetSectionAndTrackForPublicationAsync(
            PublicationListViewItemModel publication,
            LanguageListViewItemModel language,
            IFetchProgress? progress = null)
    {
        Log.Debug("GetSectionAndTrackForPublicationAsync: Starting for publication={PublicationCode}, language={LanguageCode}",
            publication.Code, language.Code);

        // Show progress while fetching
        progress?.UpdateProgress(0.1);
        
        // First, try to get sections from the database
            var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publication.Code));

        progress?.UpdateProgress(0.3);

        // If publication has sections, use sectioned flow
        if (sections != null && sections.Count > 0)
        {
            Log.Debug("GetSectionAndTrackForPublicationAsync: Found {SectionCount} sections, using sectioned flow",
                sections.Count);
            progress?.UpdateProgress(0.5);
            var sectionedResult = await sectionTrackResolver.GetFirstSectionAndTrackFromSectionsAsync(
                language.Code,
                publication.Code,
                sections,
                progress);
            Log.Debug("GetSectionAndTrackForPublicationAsync: Sectioned result: sectionNumber={SectionNumber}, trackNumber={TrackNumber}, sectionName={SectionName}, trackTitle={TrackTitle}",
                sectionedResult.SectionNumber, sectionedResult.TrackNumber, sectionedResult.SectionName, sectionedResult.TrackTitle);
            
            // Warn if names are empty but numbers are valid
            if (sectionedResult.SectionNumber > 0 && string.IsNullOrWhiteSpace(sectionedResult.SectionName))
            {
                Log.Warning("GetSectionAndTrackForPublicationAsync: SectionName is empty for sectionNumber={SectionNumber}", 
                    sectionedResult.SectionNumber);
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
        var result = await sectionTrackResolver.GetFirstTrackForNonSectionedAsync(language.Code, publication.Code, progress);
        Log.Debug("GetSectionAndTrackForPublicationAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            result.TrackNumber, result.TrackTitle);
        return result;
    }

    /// <summary>
    /// When user selects a language, cascade to get first publication, section, and track.
    /// Dynamically detects if publication has sections by querying the database.
    /// </summary>
    public async Task<(string? PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPublicationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language, IFetchProgress? progress = null)
    {
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Starting for language={LanguageCode}", language.Code);

        progress?.UpdateProgress(0.1);

        // Step 1: Get publications and find the first one that can be queried with a language
        // Some publications (like "iam" for Music) have LanguageId = NULL and can't be queried with a language
        var stateValue = state.Value;
        var categoryName = stateValue.CurrentSchedule?.BiblePublicationCategoryName;
        
        // Get all available publications for this language and category
        var publications = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code, categoryName, downloadAll: false, progress));

        if (publications == null || publications.Count == 0)
        {
            Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: No publications found for language={LanguageCode}, category={CategoryName}", 
                language.Code, categoryName ?? "(null)");
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        // Find the first publication - prioritize publications without LanguageId (like "iam" for Music)
        // Publications without LanguageId don't need a language, so the language row should be hidden
        string? publicationCode = null;
        BiblePublication? publication = null;
        bool publicationWithoutLanguage = false;
        
        (publicationCode, publication, publicationWithoutLanguage) = await publicationChooser.ChooseAsync(publications, language, progress);

        if (string.IsNullOrEmpty(publicationCode) || publication == null)
        {
            Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: No publication found for language={LanguageCode}, category={CategoryName}", 
                language.Code, categoryName ?? "(null)");
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var publicationName = publication.Name;

        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Selected publication code={PublicationCode}, name={PublicationName}, withoutLanguage={WithoutLanguage}",
            publicationCode, publicationName, publicationWithoutLanguage);

        // For publications without LanguageId, query without a language code
        // For publications with LanguageId, use the language code
        string? languageCodeForQuery = publicationWithoutLanguage ? null : language.Code;

        // Ensure publication is harvested before getting sections
        // For publications with LanguageId, EnsurePublicationExistsAsync should have been called above,
        // but we ensure it here as well to handle edge cases
        if (!publicationWithoutLanguage && languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Ensuring publication {PublicationCode} exists for language {LanguageCode} before getting sections",
                publicationCode, language.Code);
            progress?.UpdateProgress(0.5);
            try
            {
                await languageContentService.EnsurePublicationExistsAsync(publicationCode, language.Code, default, progress);
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
        SortedDictionary<int, BiblePublicationSection>? sections = null;
        if (publicationWithoutLanguage)
        {
            // Query sections for publications without language (LanguageId=null)
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Querying sections for publication without language={PublicationCode}", publicationCode);
            sections = await Task.Run(async () =>
                await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode));
        }
        else
        {
            // Query sections directly from database without triggering EnsureAllSectionsForPublicationAsync
            // This avoids fetching all sections when we only need to check if the publication is sectioned
            if (biblePublicationSectionService != null)
            {
                sections = await Task.Run(async () =>
                    await biblePublicationSectionService.GetSectionsByPublicationAsync(language.Code, publicationCode, default));
                
                // If no sections found and publication was just harvested, it might be a non-sectioned publication
                // Or the publication might not exist yet - in that case, EnsurePublicationExistsAsync above should have created it
                // For sectioned publications, EnsurePublicationExistsAsync fetches all sections, so we should have sections now
                if (sections == null || sections.Count == 0)
                {
                    Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found in database after ensuring publication exists, publication is likely non-sectioned");
                }
            }
            else
            {
                // Fallback to MediaService method (but this will trigger full harvesting)
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: biblePublicationSectionService is null, using MediaService.GetBiblePublicationSections (will trigger full harvesting)");
                sections = await Task.Run(async () =>
                    await mediaService.GetBiblePublicationSections(language.Code, publicationCode));
            }
        }

        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get first section and track
            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Found {SectionCount} sections, using sectioned flow", sections.Count);
            progress?.UpdateProgress(0.7);
            // For publications without language, pass null as language code
            var (sectionNumber, firstTrackNumber, sectionName, firstTrackTitle) =
                await sectionTrackResolver.GetFirstSectionAndTrackFromSectionsAsync(languageCodeForQuery, publicationCode, sections, progress);

            Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Sectioned result: sectionNumber={SectionNumber}, sectionName={SectionName}, trackNumber={TrackNumber}, trackTitle={TrackTitle}",
                sectionNumber, sectionName, firstTrackNumber, firstTrackTitle);

            // Warn if names are empty but numbers are valid
            if (sectionNumber > 0 && string.IsNullOrWhiteSpace(sectionName))
            {
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: SectionName is empty for sectionNumber={SectionNumber}", 
                    sectionNumber);
            }
            if (firstTrackNumber > 0 && string.IsNullOrWhiteSpace(firstTrackTitle))
            {
                Log.Warning("GetPublicationSectionAndTrackForLanguageAsync: TrackTitle is empty for trackNumber={TrackNumber}", 
                    firstTrackNumber);
            }

            return (publicationCode, sectionNumber, firstTrackNumber, sectionName, publicationName, firstTrackTitle);
        }

        // Non-sectioned publication (drama/video) - get first track directly
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: No sections found, using non-sectioned flow");
        progress?.UpdateProgress(0.7);
        // For publications without language, pass null/empty as language code
        // GetFirstTrackForNonSectionedAsync will need to handle this case
        var (_, trackNumber, _, trackTitle) = await sectionTrackResolver.GetFirstTrackForNonSectionedAsync(languageCodeForQuery, publicationCode, progress);
        Log.Debug("GetPublicationSectionAndTrackForLanguageAsync: Non-sectioned result: trackNumber={TrackNumber}, trackTitle={TrackTitle}",
            trackNumber, trackTitle);
        return (publicationCode, 0, trackNumber, string.Empty, publicationName, trackTitle);
    }
}
