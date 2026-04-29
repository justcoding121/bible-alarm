#nullable enable
using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.CategorySelectionAutoPopulateHandlerHelpers;
using Bible.Alarm.Stores.Messages.CategoryProgress;
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
    private readonly ILanguageNameService languageNameService;
    private readonly BiblePublicationSelectionItemSelector itemSelector;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    public CategorySelectionAutoPopulateHandler(
        IBiblePublicationService biblePublicationService,
        IMediaService mediaService,
        ILanguageContentService languageContentService,
        ILanguageNameService languageNameService,
        BiblePublicationSelectionItemSelector itemSelector,
        IState<ApplicationState> state,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        this.biblePublicationService = biblePublicationService;
        this.mediaService = mediaService;
        this.languageContentService = languageContentService;
        this.languageNameService = languageNameService;
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

        var fetchOccurred = false;

        try
        {
            logger.Information("CategorySelectionAutoPopulateHandler: Starting auto-population for category={CategoryName}",
                action.CategoryName);

            // Progress will only be reported when fetches actually happen (via progress tracker)
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                logger.Warning("CategorySelectionAutoPopulateHandler: CurrentSchedule is null, skipping auto-population");
                return;
            }

            // Step 1: Try to preserve current language if it has publications in the new category, otherwise fallback to English
            // Get all languages for this category from PublicationLanguage table (DB query only, no progress)
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
                    using var langEnumerator = languages.Values.GetEnumerator();
                    _ = langEnumerator.MoveNext();
                    selectedLanguage = langEnumerator.Current;
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
            // Progress will be reported only when fetches happen
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
                    query = query.Where(pl => pl.Category != null && pl.Category.CategoryCode == action.CategoryName);
                }
                
                // Get publications, then sort by priority (nwt first, then bi12, then others)
                var publicationLanguages = await query
                    .ToListAsync();
                
                // Sort by priority for the selected category (Bible: nwt first; Music: osg first), then by ID as tiebreaker
                publicationLanguages = publicationLanguages
                    .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.GetPublicationCodeComparerForCategory(action.CategoryName))
                    .ThenBy(pl => pl.Id)
                    .ToList();

                // Try each publication: check if already cataloged, catalog if needed, then verify it can be queried
                foreach (var pl in publicationLanguages)
                {
                    // For dramas, use case-sensitive publication codes in DB (Dramas vs DramaticBibleReadings).
                    // For others (e.g. DramasGoodNews), preserve exact case.
                    var lowerCode = pl.PublicationCode.ToLowerInvariant();
                    var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
                    var publicationCodeForDb = isDrama
                        ? (lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                            ? AppConstants.Media.BiblePublicationCategoryDramas
                            : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings)
                        : pl.PublicationCode;

                    var isAlreadyCataloged = await CategorySelectionAutoPopulateCatalogCheck.CheckIfPublicationWithFirstSectionCatalogedAsync(
                        logger, db, pl.PublicationCode, normalizedLanguageCode);
                    
                    if (!isAlreadyCataloged)
                    {
                        fetchOccurred = true;

                        // Try to catalog the publication (EnsurePublicationExistsAsync checks if it exists first)
                        // Progress will be reported via CategoryFetchProgressMessage when fetch actually happens
                        var catalogProgressReporter = new Bible.Alarm.Common.Helpers.CategoryFetchProgressReporter(
                            action.CategoryId, default);

                        var isCataloged = await languageContentService.EnsurePublicationExistsAsync(
                            pl.PublicationCode, selectedLanguage.LanguageCode, catalogProgressReporter);
                        
                        if (!isCataloged)
                        {
                            logger.Debug("CategorySelectionAutoPopulateHandler: Failed to catalog publication={PublicationCode} for language={LanguageCode}, trying next",
                                pl.PublicationCode, selectedLanguage.LanguageCode);
                            continue;
                        }
                    }
                    else
                    {
                        // Publication already cataloged - no fetch needed, so no progress update
                        logger.Debug("CategorySelectionAutoPopulateHandler: Publication={PublicationCode} for language={LanguageCode} already cataloged with first section and tracks",
                            pl.PublicationCode, selectedLanguage.LanguageCode);
                    }
                    
                    // Verify the publication can be queried with the language (has LanguageId).
                    // IMPORTANT: Re-check the DB after a catalog; a precomputed snapshot will be stale.
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
                        logger.Debug("CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode} (cataloged and can be queried with language={LanguageCode})",
                            publicationCode, selectedLanguage.LanguageCode);
                        break;
                    }
                    else
                    {
                        logger.Debug("CategorySelectionAutoPopulateHandler: Publication={PublicationCode} cataloged but cannot be queried with language={LanguageCode} (may not have LanguageId), trying next",
                            pl.PublicationCode, selectedLanguage.LanguageCode);
                    }
                }
            }
            
            // Step 3: If no publication with LanguageId found, try publications without LanguageId
            if (string.IsNullOrEmpty(publicationCode))
            {
                if (!string.IsNullOrWhiteSpace(action.CategoryName))
                {
                    var categoryComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(action.CategoryName);
                    var pubWithoutLanguage = (await db.BiblePublications
                        .AsNoTracking()
                        .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == action.CategoryName) &&
                                    bp.LanguageId == null)
                        .ToListAsync())
                        .OrderBy(bp => bp.PublicationCode, categoryComparer)
                        .ThenBy(bp => bp.Id)
                        .FirstOrDefault();
                    
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
                logger.Warning("CategorySelectionAutoPopulateHandler: No publication found or cataloged for language={LanguageCode}, category={CategoryName}",
                    languageCodeForWarning, action.CategoryName);
                return;
            }

            logger.Debug("CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode}, withoutLanguage={WithoutLanguage}",
                publicationCode, publicationWithoutLanguage);

            // Step 4: Get first section and track for the publication
            string? sectionCode = null;
            string trackCode = string.Empty;
            string sectionName = string.Empty;
            string publicationName = string.Empty;
            string trackTitle = string.Empty;

            if (publicationWithoutLanguage)
            {
                var (secCode, trkCode, secName, trkTitle, pubName) = await BiblePublicationCascadeNoLanguageResolver.GetFirstSectionAndTrackAsync(
                    mediaService, scopeFactory, publicationCode);
                sectionCode = secCode;
                trackCode = trkCode ?? string.Empty;
                sectionName = secName;
                trackTitle = trkTitle;
                publicationName = pubName;
            }
            else
            {
                // For publications with LanguageId, use the language-based flow
                if (selectedLanguage == null)
                {
                    logger.Warning("CategorySelectionAutoPopulateHandler: selectedLanguage is null but publication requires language");
                    return;
                }

                var languageName = await languageNameService.GetNameAsync(selectedLanguage.Id, AppConstants.Media.DefaultLanguageCode)
                    ?? selectedLanguage.LanguageCode;
                var languageModel = new LanguageListViewItemModel(selectedLanguage, languageName);
                // GetPublicationSectionAndTrackForLanguageAsync reads from DB and resolves names (no fetch, no progress)
                // Pass action.CategoryName so the method filters by the new category (state hasn't been updated yet).
                var (resultPublicationCode, resultSectionCode, resultTrackCode, resultSectionName, resultPublicationName, resultTrackTitle) =
                    await itemSelector.GetPublicationSectionAndTrackForLanguageAsync(languageModel, categoryNameOverride: action.CategoryName);

                if (string.IsNullOrEmpty(resultPublicationCode) || string.IsNullOrWhiteSpace(resultTrackCode))
                {
                    logger.Warning("CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}, language={LanguageCode}",
                        publicationCode, selectedLanguage.LanguageCode);
                    return;
                }

                // Use the publication code and name from the result (may differ for dramas)
                publicationCode = resultPublicationCode;
                sectionCode = string.IsNullOrWhiteSpace(resultSectionCode) ? null : resultSectionCode;
                trackCode = resultTrackCode;
                sectionName = resultSectionName;
                publicationName = resultPublicationName;
                trackTitle = resultTrackTitle;
            }

            if (string.IsNullOrWhiteSpace(trackCode))
            {
                logger.Warning("CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}",
                    publicationCode);
                return;
            }

            var languageCodeForLog = publicationWithoutLanguage ? "N/A" : (selectedLanguage?.LanguageCode ?? "N/A");
            logger.Information("CategorySelectionAutoPopulateHandler: Auto-populated - Language={LanguageCode}, Publication={PublicationCode}, Section={SectionCode}, Track={TrackCode}, WithoutLanguage={WithoutLanguage}",
                languageCodeForLog, publicationCode, sectionCode, trackCode, publicationWithoutLanguage);

            // Step 6: Update schedule with selected values
            var updatedSchedule = currentSchedule.DeepClone();
            
            // Set category from action - this is the ONLY place category should be set
            updatedSchedule.BiblePublicationCategoryId = action.CategoryId;
            updatedSchedule.BiblePublicationCategoryName = action.CategoryName;
            // Music container visibility: hide when main content is music (category Music), show otherwise
            updatedSchedule.BiblePublicationIsMusic = string.Equals(action.CategoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase);

            // For publications without language (LanguageId == null), set language to English default.
            // This ensures cascade consistency: the language row shows "English" and publications modal
            // will show English publications + non-languaged publications.
            // For publications with language, set language fields from selectedLanguage.
            if (publicationWithoutLanguage)
            {
                updatedSchedule.BiblePublicationLanguageCode = AppConstants.Media.DefaultLanguageCode;
                updatedSchedule.BiblePublicationLanguageName = null;
                updatedSchedule.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                logger.Debug("CategorySelectionAutoPopulateHandler: Setting language to English default for publication without LanguageId={PublicationCode}",
                    publicationCode);
            }
            else if (selectedLanguage != null)
            {
                updatedSchedule.BiblePublicationLanguageCode = selectedLanguage.LanguageCode;
                updatedSchedule.BiblePublicationLanguageName = languageNameService.GetNameCached(selectedLanguage.Id) ?? selectedLanguage.LanguageCode;
                updatedSchedule.BiblePublicationLanguageDirection = selectedLanguage.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
            }
            
            updatedSchedule.BiblePublicationCode = publicationCode;
            updatedSchedule.BiblePublicationName = publicationName;
            updatedSchedule.BiblePublicationSectionCode = Bible.Alarm.Shared.Helpers.SectionCodeHelper.Normalize(sectionCode);
            updatedSchedule.BiblePublicationSectionName = sectionName;
            updatedSchedule.BiblePublicationTrackCode = trackCode;
            updatedSchedule.BiblePublicationTrackTitle = trackTitle;
            // Reset saved progress when category changes so stale seek positions
            // don't carry over to a different publication/track.
            updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;

            // Dispatch action to update the schedule
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));

            if (fetchOccurred)
            {
                ReportProgress(1.0, isComplete: true);
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
                    new CategoryFetchProgress { CategoryId = action.CategoryId, Progress = -1, IsComplete = true, HasError = false }));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            logger.Warning(ex, "CategorySelectionAutoPopulateHandler: Network error during auto-population for category={CategoryName}",
                action.CategoryName);
            WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
                new CategoryFetchProgress { CategoryId = action.CategoryId, Progress = 0, IsComplete = true, HasError = true }));

            if (action.PreviousScheduleSnapshot != null)
            {
                logger.Information("CategorySelectionAutoPopulateHandler: Reverting to previous schedule state after network error");
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(action.PreviousScheduleSnapshot, true, true, shouldSave: false));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "CategorySelectionAutoPopulateHandler: Error during auto-population for category={CategoryName}",
                action.CategoryName);
            WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(
                new CategoryFetchProgress { CategoryId = action.CategoryId, Progress = 0, IsComplete = true, HasError = true }));

            if (action.PreviousScheduleSnapshot != null)
            {
                logger.Information("CategorySelectionAutoPopulateHandler: Reverting to previous schedule state after error");
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(action.PreviousScheduleSnapshot, true, true, shouldSave: false));
            }
        }
    }
}
