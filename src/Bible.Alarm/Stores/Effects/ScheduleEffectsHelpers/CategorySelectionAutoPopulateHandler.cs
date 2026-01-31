#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

public sealed class CategorySelectionAutoPopulateHandler
{
    private readonly IBiblePublicationService biblePublicationService;
    private readonly IMediaService mediaService;
    private readonly ILanguageContentService languageContentService;
    private readonly BiblePublicationSelectionItemSelector itemSelector;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    public CategorySelectionAutoPopulateHandler(
        IBiblePublicationService biblePublicationService,
        IMediaService mediaService,
        ILanguageContentService languageContentService,
        BiblePublicationSelectionItemSelector itemSelector,
        IState<ApplicationState> state,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        this.biblePublicationService = biblePublicationService;
        this.mediaService = mediaService;
        this.languageContentService = languageContentService;
        this.itemSelector = itemSelector;
        this.state = state;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    public async Task HandleAsync(CategorySelectionAction action, IDispatcher dispatcher)
    {
        try
        {
            logger.Information("CategorySelectionAutoPopulateHandler: Starting auto-population for category={CategoryName}",
                action.CategoryName);

            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning("CategorySelectionAutoPopulateHandler: CurrentSchedule is null, skipping auto-population");
                return;
            }

            // Step 1: Try to preserve current language if it has publications in the new category, otherwise fallback to English
            // Get all languages for this category from PublicationLanguage table
            var languages = await biblePublicationService.GetDistinctLanguagesAsync(action.CategoryName);
            
            Language? selectedLanguage = null;
            
            // First, try to preserve the previous language if it has publications in the new category
            if (!string.IsNullOrWhiteSpace(action.PreviousLanguageCode))
            {
                var previousLanguageCode = action.PreviousLanguageCode.ToUpperInvariant();
                if (languages.TryGetValue(previousLanguageCode, out var previousLanguage))
                {
                    // GetDistinctLanguagesAsync(category) is already derived from PublicationLanguages for that category.
                    // If the language is present here, it has at least one publication in the category.
                    // Avoid an extra DB query in this hot path.
                    selectedLanguage = previousLanguage;
                    logger.Debug("CategorySelectionAutoPopulateHandler: Preserving previous language={LanguageCode} (has publications in new category={CategoryName})",
                        previousLanguageCode, action.CategoryName);
                }
            }
            
            // Fallback to English "E" if previous language couldn't be preserved
            if (selectedLanguage == null)
            {
                if (languages.TryGetValue("E", out var englishLanguage))
                {
                    selectedLanguage = englishLanguage;
                    logger.Debug("CategorySelectionAutoPopulateHandler: Selected English language (default/fallback)");
                }
                else if (languages.Count > 0)
                {
                    // Fallback to first available language if English not found
                    selectedLanguage = languages.Values.First();
                    logger.Debug("CategorySelectionAutoPopulateHandler: English not found, selected first available language={LanguageCode}",
                        selectedLanguage.LanguageCode);
                }
                else
                {
                    logger.Warning("CategorySelectionAutoPopulateHandler: No languages found for category={CategoryName}",
                        action.CategoryName);
                    // Continue anyway - we'll check for publications without LanguageId
                }
            }

            // Step 2: Find first publication - try publications with LanguageId for selected language, then publications without LanguageId
            string? publicationCode = null;
            bool publicationWithoutLanguage = false;
            
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            // First, try to find a publication with LanguageId for the selected language (prefer English)
            if (selectedLanguage != null)
            {
                var normalizedLanguageCode = selectedLanguage.LanguageCode.ToUpperInvariant();
                
                // Get first publication by ID order from PublicationLanguages
                var query = db.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Language)
                    .Include(pl => pl.Category)
                    .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode);
                
                // Filter by category if provided
                if (!string.IsNullOrWhiteSpace(action.CategoryName))
                {
                    query = query.Where(pl => pl.Category != null && pl.Category.CategoryName == action.CategoryName);
                }
                
                // Get publications, then sort by priority (nwt first, then bi12, then others)
                var publicationLanguages = await query
                    .ToListAsync();
                
                // Sort by priority: nwt first, then bi12, then others, then by ID as tiebreaker
                publicationLanguages = publicationLanguages
                    .OrderBy(pl => PublicationSortHelper.GetPublicationSortPriority(pl.PublicationCode))
                    .ThenBy(pl => pl.Id)
                    .ToList();

                // Avoid N+1: batch-load which publications are present with LanguageId for this language.
                var candidateCodes = publicationLanguages
                    .Select(pl => pl.PublicationCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var codesWithLanguageId = await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp => candidateCodes.Contains(bp.PublicationCode) &&
                                 bp.LanguageId != null &&
                                 bp.Language != null &&
                                 bp.Language.LanguageCode == normalizedLanguageCode)
                    .Select(bp => bp.PublicationCode)
                    .Distinct()
                    .ToListAsync();

                var codesWithLanguageIdSet = codesWithLanguageId.ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                // Try each publication: check if already harvested, harvest if needed, then verify it can be queried
                foreach (var pl in publicationLanguages)
                {
                    // Check if publication with first section and tracks is already harvested
                    var isAlreadyHarvested = await CheckIfPublicationWithFirstSectionHarvestedAsync(
                        db, pl.PublicationCode, normalizedLanguageCode);
                    
                    if (!isAlreadyHarvested)
                    {
                        // Try to harvest the publication (EnsurePublicationExistsAsync checks if it exists first)
                        var isHarvested = await languageContentService.EnsurePublicationExistsAsync(
                            pl.PublicationCode, selectedLanguage.LanguageCode);
                        
                        if (!isHarvested)
                        {
                            logger.Debug("CategorySelectionAutoPopulateHandler: Failed to harvest publication={PublicationCode} for language={LanguageCode}, trying next",
                                pl.PublicationCode, selectedLanguage.LanguageCode);
                            continue;
                        }
                    }
                    else
                    {
                        logger.Debug("CategorySelectionAutoPopulateHandler: Publication={PublicationCode} for language={LanguageCode} already harvested with first section and tracks",
                            pl.PublicationCode, selectedLanguage.LanguageCode);
                    }
                    
                    // Verify the publication can be queried with the language (has LanguageId)
                    var canQueryWithLanguage = codesWithLanguageIdSet.Contains(pl.PublicationCode);
                    
                    if (canQueryWithLanguage)
                    {
                        publicationCode = pl.PublicationCode;
                        publicationWithoutLanguage = false;
                        logger.Debug("CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode} (harvested and can be queried with language={LanguageCode})",
                            publicationCode, selectedLanguage.LanguageCode);
                        break;
                    }
                    else
                    {
                        logger.Debug("CategorySelectionAutoPopulateHandler: Publication={PublicationCode} harvested but cannot be queried with language={LanguageCode} (may not have LanguageId), trying next",
                            pl.PublicationCode, selectedLanguage.LanguageCode);
                    }
                }
            }
            
            // Step 3: If no publication with LanguageId found, try publications without LanguageId
            if (string.IsNullOrEmpty(publicationCode))
            {
                if (!string.IsNullOrWhiteSpace(action.CategoryName))
                {
                    var pubWithoutLanguage = await db.BiblePublications
                        .AsNoTracking()
                        .Where(bp => bp.Category != null && 
                                    bp.Category.CategoryName == action.CategoryName &&
                                    bp.LanguageId == null)
                        .OrderBy(bp => bp.Id)
                        .FirstOrDefaultAsync();
                    
                    if (pubWithoutLanguage != null)
                    {
                        publicationCode = pubWithoutLanguage.PublicationCode;
                        publicationWithoutLanguage = true;
                        logger.Debug("CategorySelectionAutoPopulateHandler: Selected publication without LanguageId={PublicationCode}",
                            publicationCode);
                    }
                }
            }

            if (string.IsNullOrEmpty(publicationCode))
            {
                var languageCodeForWarning = selectedLanguage?.LanguageCode ?? "N/A";
                logger.Warning("CategorySelectionAutoPopulateHandler: No publication found or harvested for language={LanguageCode}, category={CategoryName}",
                    languageCodeForWarning, action.CategoryName);
                return;
            }

            logger.Debug("CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode}, withoutLanguage={WithoutLanguage}",
                publicationCode, publicationWithoutLanguage);

            // Step 4: Get first section and track for the publication
            int sectionIndex = 0;
            string? sectionCode = null;
            int trackNumber = 0;
            string sectionName = string.Empty;
            string publicationName = string.Empty;
            string trackTitle = string.Empty;

            if (publicationWithoutLanguage)
            {
                // For publications without LanguageId, query directly without a language
                // Get publication name from database
                using (var nameScope = scopeFactory.CreateScope())
                {
                    var nameDb = nameScope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var pub = await nameDb.BiblePublications
                        .AsNoTracking()
                        .Where(bp => bp.PublicationCode == publicationCode && bp.LanguageId == null)
                        .FirstOrDefaultAsync();
                    publicationName = pub?.Name ?? publicationCode;
                }

                // Get sections for music publications (Category=Music, LanguageId=null)
                var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
                
                if (sections != null && sections.Count > 0)
                {
                    // Sectioned publication - get first section and track
                    var firstSectionKvp = sections.First();
                    var firstSection = firstSectionKvp.Value;
                    sectionIndex = firstSectionKvp.Key;
                    sectionCode = firstSection.SectionCode;
                    sectionName = firstSection.Name;

                    // Get tracks for the first section - query directly from database
                    using (var trackScope = scopeFactory.CreateScope())
                    {
                        var trackDb = trackScope.ServiceProvider.GetRequiredService<MediaDbContext>();
                        var pub = await trackDb.BiblePublications
                            .AsNoTracking()
                            .Include(x => x.Sections)
                                .ThenInclude(s => s.Tracks)
                            .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                            .FirstOrDefaultAsync();
                        
                        if (pub?.Sections != null)
                        {
                            var section = pub.Sections.FirstOrDefault(s => s.SectionCode == firstSection.SectionCode);
                            if (section?.Tracks != null && section.Tracks.Count > 0)
                            {
                                var firstTrack = section.Tracks.OrderBy(t => t.Number).First();
                                trackNumber = firstTrack.Number;
                                trackTitle = firstTrack.Title ?? string.Empty;
                            }
                        }
                    }
                }
                else
                {
                    // Non-sectioned publication - get first track directly
                    // Query publication without LanguageId
                    using (var trackScope = scopeFactory.CreateScope())
                    {
                        var trackDb = trackScope.ServiceProvider.GetRequiredService<MediaDbContext>();
                        var pub = await trackDb.BiblePublications
                            .AsNoTracking()
                            .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                            .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                            .FirstOrDefaultAsync();
                        
                        if (pub?.Tracks != null && pub.Tracks.Count > 0)
                        {
                            var firstTrack = pub.Tracks.OrderBy(t => t.Number).First();
                            trackNumber = firstTrack.Number;
                            trackTitle = firstTrack.Title ?? string.Empty;
                        }
                    }
                }
            }
            else
            {
                // For publications with LanguageId, use the language-based flow
                if (selectedLanguage == null)
                {
                    logger.Warning("CategorySelectionAutoPopulateHandler: selectedLanguage is null but publication requires language");
                    return;
                }

                var languageModel = new LanguageListViewItemModel(selectedLanguage);
                // Note: Progress is not passed here because the effect handler runs asynchronously
                // Progress is shown in the ViewModel while waiting for state changes
                var (resultPublicationCode, resultSectionCode, resultTrackNumber, resultSectionName, resultPublicationName, resultTrackTitle) =
                    await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(languageModel);

                if (string.IsNullOrEmpty(resultPublicationCode) || resultTrackNumber <= 0)
                {
                    logger.Warning("CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}, language={LanguageCode}",
                        publicationCode, selectedLanguage.LanguageCode);
                    return;
                }

                // Use the publication code and name from the result (may differ for dramas)
                publicationCode = resultPublicationCode;
                sectionIndex = resultSectionCode;
                sectionCode = resultSectionCode > 0 ? resultSectionCode.ToString() : null;
                trackNumber = resultTrackNumber;
                sectionName = resultSectionName;
                publicationName = resultPublicationName;
                trackTitle = resultTrackTitle;
            }

            if (trackNumber <= 0)
            {
                logger.Warning("CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}",
                    publicationCode);
                return;
            }

            var languageCodeForLog = publicationWithoutLanguage ? "N/A" : (selectedLanguage?.LanguageCode ?? "N/A");
            logger.Information("CategorySelectionAutoPopulateHandler: Auto-populated - Language={LanguageCode}, Publication={PublicationCode}, Section={SectionCode}, Track={TrackNumber}, WithoutLanguage={WithoutLanguage}",
                languageCodeForLog, publicationCode, sectionCode, trackNumber, publicationWithoutLanguage);

            // Step 6: Update schedule with selected values
            var updatedSchedule = currentSchedule.DeepClone();
            
            // Set category from action - this is the ONLY place category should be set
            updatedSchedule.BiblePublicationCategoryId = action.CategoryId;
            updatedSchedule.BiblePublicationCategoryName = action.CategoryName;
            
            // Always set language to "E" (English) when category changes, even if publication doesn't have LanguageId
            // The publication list will show both publications for "E" and publications with null LanguageId
            if (selectedLanguage != null)
            {
                updatedSchedule.BiblePublicationLanguageCode = selectedLanguage.LanguageCode;
                updatedSchedule.BiblePublicationLanguageName = selectedLanguage.Name;
                updatedSchedule.BiblePublicationLanguageDirection = selectedLanguage.Direction ?? "ltr";
            }
            else
            {
                // Fallback: if no language found, clear language fields only if publication doesn't have LanguageId
                if (publicationWithoutLanguage)
                {
                    updatedSchedule.BiblePublicationLanguageCode = null;
                    updatedSchedule.BiblePublicationLanguageName = null;
                    updatedSchedule.BiblePublicationLanguageDirection = null;
                    logger.Debug("CategorySelectionAutoPopulateHandler: Clearing language fields for publication without LanguageId={PublicationCode} (no languages available)",
                        publicationCode);
                }
            }
            
            updatedSchedule.BiblePublicationCode = publicationCode;
            updatedSchedule.BiblePublicationName = publicationName;
            updatedSchedule.BiblePublicationSectionCode = !string.IsNullOrWhiteSpace(sectionCode)
                ? sectionCode
                : (sectionIndex > 0 ? sectionIndex.ToString() : null);
            updatedSchedule.BiblePublicationSectionName = sectionName;
            updatedSchedule.BiblePublicationTrackNumber = trackNumber;
            updatedSchedule.BiblePublicationTrackTitle = trackTitle;
            updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;

            // Dispatch action to update the schedule
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "CategorySelectionAutoPopulateHandler: Error during auto-population for category={CategoryName}",
                action.CategoryName);
        }
    }

    /// <summary>
    /// Checks if a publication with its first section and tracks is already harvested.
    /// </summary>
    private async Task<bool> CheckIfPublicationWithFirstSectionHarvestedAsync(
        MediaDbContext db,
        string publicationCode,
        string normalizedLanguageCode)
    {
        try
        {
            // For dramas, use case-sensitive publication codes
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = publicationCode;
            }

            // Check if publication exists (fast path: just Id)
            var publicationId = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.PublicationCode == publicationCodeForDb &&
                             bp.Language != null &&
                             bp.Language.LanguageCode == normalizedLanguageCode)
                .Select(bp => bp.Id)
                .FirstOrDefaultAsync();

            if (publicationId <= 0)
            {
                return false;
            }

            // Get first section code from SectionLanguages
            var firstSectionCode = await db.SectionLanguages
                .AsNoTracking()
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                             sl.Language != null &&
                             sl.Language.LanguageCode == normalizedLanguageCode)
                .OrderBy(sl => sl.SectionCode, SectionCodeHelper.SectionCodeComparer)
                .Select(sl => sl.SectionCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(firstSectionCode))
            {
                // No sections defined - check if it's a flat publication (has tracks directly)
                var hasTracks = await db.BiblePublicationTracks
                    .AsNoTracking()
                    .AnyAsync(t => t.BiblePublicationId == publicationId &&
                                   t.BiblePublicationSectionId == null);
                return hasTracks;
            }

            // Check if the first section exists
            var firstSectionId = await db.BiblePublicationSections
                .AsNoTracking()
                .Where(s => s.BiblePublicationId == publicationId &&
                            s.SectionCode.Equals(firstSectionCode, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSectionId <= 0)
            {
                return false;
            }

            // Check if the first section has at least one track
            return await db.BiblePublicationTracks
                .AsNoTracking()
                .AnyAsync(t => t.BiblePublicationId == publicationId &&
                               t.BiblePublicationSectionId == firstSectionId);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CategorySelectionAutoPopulateHandler: Error checking if publication {PublicationCode} is harvested",
                publicationCode);
            return false;
        }
    }
}
