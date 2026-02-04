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
using Bible.Alarm.Stores.Messages;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Messaging;
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
        void ReportProgress(double progress, bool isComplete = false)
        {
            WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
                new CategoryFetchProgress
                {
                    CategoryId = action.CategoryId,
                    Progress = progress,
                    IsComplete = isComplete
                }));
        }

        try
        {
            logger.Information("CategorySelectionAutoPopulateHandler: Starting auto-population for category={CategoryName}",
                action.CategoryName);

            ReportProgress(0.0);

            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning("CategorySelectionAutoPopulateHandler: CurrentSchedule is null, skipping auto-population");
                ReportProgress(1.0, isComplete: true);
                return;
            }

            // Step 1: Try to preserve current language if it has publications in the new category, otherwise fallback to English
            // Get all languages for this category from PublicationLanguage table
            ReportProgress(0.05);
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
            ReportProgress(0.10);
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
                    .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.PublicationCodeComparer)
                    .ThenBy(pl => pl.Id)
                    .ToList();

                // Try each publication: check if already harvested, harvest if needed, then verify it can be queried
                foreach (var pl in publicationLanguages)
                {
                    // For dramas, use case-sensitive publication codes in DB ("Dramas"/"DramaticBibleReadings").
                    // For others (e.g. gnj), preserve exact case.
                    var lowerCode = pl.PublicationCode.ToLowerInvariant();
                    var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
                    var publicationCodeForDb = isDrama
                        ? (lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                            ? "Dramas"
                            : "DramaticBibleReadings")
                        : pl.PublicationCode;

                    // Check if publication with first section and tracks is already harvested
                    var isAlreadyHarvested = await CheckIfPublicationWithFirstSectionHarvestedAsync(
                        db, pl.PublicationCode, normalizedLanguageCode);
                    
                    if (!isAlreadyHarvested)
                    {
                        // Try to harvest the publication (EnsurePublicationExistsAsync checks if it exists first)
                        // Progress mapping: internal 0-1 maps to 0.10-0.90 in overall flow
                        // - Internal 0.5 (pub+section saved) -> 0.50 overall
                        // - Internal 1.0 (tracks saved) -> 0.90 overall
                        ReportProgress(0.10);
                        
                        // Create a progress tracker that maps internal progress to overall progress
                        var harvestProgressTracker = new Bible.Alarm.Common.Helpers.FetchProgressTracker(
                            internalProgress =>
                            {
                                // Map 0-1 to 0.10-0.90
                                var mappedProgress = 0.10 + (internalProgress * 0.80);
                                ReportProgress(mappedProgress);
                            },
                            _ => { },
                            _ => { });
                        
                        var isHarvested = await languageContentService.EnsurePublicationExistsAsync(
                            pl.PublicationCode, selectedLanguage.LanguageCode, default, harvestProgressTracker);
                        
                        if (!isHarvested)
                        {
                            logger.Debug("CategorySelectionAutoPopulateHandler: Failed to harvest publication={PublicationCode} for language={LanguageCode}, trying next",
                                pl.PublicationCode, selectedLanguage.LanguageCode);
                            continue;
                        }
                    }
                    else
                    {
                        // Publication already harvested - report 90% (all data saved)
                        ReportProgress(0.90);
                        logger.Debug("CategorySelectionAutoPopulateHandler: Publication={PublicationCode} for language={LanguageCode} already harvested with first section and tracks",
                            pl.PublicationCode, selectedLanguage.LanguageCode);
                    }
                    
                    // Verify the publication can be queried with the language (has LanguageId).
                    // IMPORTANT: Re-check the DB after a harvest; a precomputed snapshot will be stale.
                    var canQueryWithLanguage = await db.BiblePublications
                        .AsNoTracking()
                        .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                                        bp.LanguageId != null &&
                                        bp.Language != null &&
                                        bp.Language.LanguageCode == normalizedLanguageCode);
                    
                    if (canQueryWithLanguage)
                    {
                        publicationCode = publicationCodeForDb;
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
                ReportProgress(1.0, isComplete: true);
                return;
            }

            logger.Debug("CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode}, withoutLanguage={WithoutLanguage}",
                publicationCode, publicationWithoutLanguage);

            // Step 4: Get first section and track for the publication
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
                // Publication+section+tracks already saved during harvest (0.90)
                // GetPublicationSectionAndTrackForLanguageAsync reads from DB and resolves names
                var (resultPublicationCode, resultSectionCode, resultTrackNumber, resultSectionName, resultPublicationName, resultTrackTitle) =
                    await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(languageModel);
                // Data read from DB, ready to update state
                ReportProgress(0.95);

                if (string.IsNullOrEmpty(resultPublicationCode) || resultTrackNumber <= 0)
                {
                    logger.Warning("CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}, language={LanguageCode}",
                        publicationCode, selectedLanguage.LanguageCode);
                    ReportProgress(1.0, isComplete: true);
                    return;
                }

                // Use the publication code and name from the result (may differ for dramas)
                publicationCode = resultPublicationCode;
                sectionCode = string.IsNullOrWhiteSpace(resultSectionCode) ? null : resultSectionCode;
                trackNumber = resultTrackNumber;
                sectionName = resultSectionName;
                publicationName = resultPublicationName;
                trackTitle = resultTrackTitle;
            }

            if (trackNumber <= 0)
            {
                logger.Warning("CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}",
                    publicationCode);
                ReportProgress(1.0, isComplete: true);
                return;
            }

            // Tracks fetched (100%)
            ReportProgress(0.98);

            var languageCodeForLog = publicationWithoutLanguage ? "N/A" : (selectedLanguage?.LanguageCode ?? "N/A");
            logger.Information("CategorySelectionAutoPopulateHandler: Auto-populated - Language={LanguageCode}, Publication={PublicationCode}, Section={SectionCode}, Track={TrackNumber}, WithoutLanguage={WithoutLanguage}",
                languageCodeForLog, publicationCode, sectionCode, trackNumber, publicationWithoutLanguage);

            // Step 6: Update schedule with selected values
            var updatedSchedule = currentSchedule.DeepClone();
            
            // Set category from action - this is the ONLY place category should be set
            updatedSchedule.BiblePublicationCategoryId = action.CategoryId;
            updatedSchedule.BiblePublicationCategoryName = action.CategoryName;
            
            // For publications without language (LanguageId == null), set language to English default.
            // This ensures cascade consistency: the language row shows "English" and publications modal
            // will show English publications + non-languaged publications.
            // For publications with language, set language fields from selectedLanguage.
            if (publicationWithoutLanguage)
            {
                updatedSchedule.BiblePublicationLanguageCode = "E";
                updatedSchedule.BiblePublicationLanguageName = null;
                updatedSchedule.BiblePublicationLanguageDirection = "ltr";
                logger.Debug("CategorySelectionAutoPopulateHandler: Setting language to English default for publication without LanguageId={PublicationCode}",
                    publicationCode);
            }
            else if (selectedLanguage != null)
            {
                updatedSchedule.BiblePublicationLanguageCode = selectedLanguage.LanguageCode;
                updatedSchedule.BiblePublicationLanguageName = selectedLanguage.Name;
                updatedSchedule.BiblePublicationLanguageDirection = selectedLanguage.Direction ?? "ltr";
            }
            
            updatedSchedule.BiblePublicationCode = publicationCode;
            updatedSchedule.BiblePublicationName = publicationName;
            updatedSchedule.BiblePublicationSectionCode = Bible.Alarm.Shared.Helpers.SectionCodeHelper.Normalize(sectionCode);
            updatedSchedule.BiblePublicationSectionName = sectionName;
            updatedSchedule.BiblePublicationTrackNumber = trackNumber;
            updatedSchedule.BiblePublicationTrackTitle = trackTitle;
            // Do NOT reset progress here. Progress reset is applied only on Save.

            // Dispatch action to update the schedule
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));
            ReportProgress(1.0, isComplete: true);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "CategorySelectionAutoPopulateHandler: Error during auto-population for category={CategoryName}",
                action.CategoryName);
            ReportProgress(1.0, isComplete: true);
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
            // IMPORTANT: SectionCodeHelper.SectionCodeComparer can't be translated to SQL.
            // Load section codes first, then apply natural sort in-memory.
            var sectionCodes = await db.SectionLanguages
                .AsNoTracking()
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                             sl.Language != null &&
                             sl.Language.LanguageCode == normalizedLanguageCode)
                .Select(sl => sl.SectionCode)
                .ToListAsync();

            var firstSectionCode = sectionCodes
                .OrderBy(sc => sc, SectionCodeHelper.SectionCodeComparer)
                .FirstOrDefault();

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
