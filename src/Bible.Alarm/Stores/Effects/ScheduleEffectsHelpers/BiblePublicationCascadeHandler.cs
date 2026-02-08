#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
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
public sealed class BiblePublicationCascadeHandler
{
    private readonly IBiblePublicationService biblePublicationService;
    private readonly IMediaService mediaService;
    private readonly ILanguageContentService languageContentService;
    private readonly BiblePublicationSelectionItemSelector itemSelector;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    public BiblePublicationCascadeHandler(
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

    public async Task HandleAsync(IDispatcher dispatcher)
    {
        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            var languageCode = currentSchedule.BiblePublicationLanguageCode;
            var publicationCode = currentSchedule.BiblePublicationCode;
            var sectionCode = currentSchedule.BiblePublicationSectionCode;
            var trackCode = currentSchedule.BiblePublicationTrackCode;

            if (!string.IsNullOrWhiteSpace(languageCode) && string.IsNullOrWhiteSpace(publicationCode))
            {
                await HandleLanguageCascadeAsync(currentSchedule, dispatcher);
                // Language cascade handles everything below
                return;
            }

            if (!string.IsNullOrWhiteSpace(publicationCode) && 
                string.IsNullOrWhiteSpace(sectionCode))
            {
                await HandlePublicationCascadeAsync(currentSchedule, dispatcher);
                // Publication cascade handles section and track
                return;
            }

            if (!string.IsNullOrWhiteSpace(sectionCode) &&
                string.IsNullOrWhiteSpace(trackCode))
            {
                await HandleSectionCascadeAsync(currentSchedule, dispatcher);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "BiblePublicationCascadeHandler: Error during cascade");
        }
    }

    private async Task HandleLanguageCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.BiblePublicationLanguageCode!;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        var existingPublicationCode = currentSchedule.BiblePublicationCode;
        var publicationModalItemCount = await GetBiblePublicationModalItemCountAsync(languageCode, categoryName);

        logger.Information("BiblePublicationCascadeHandler: Language cascade - language={LanguageCode}, category={CategoryName}, existingPublication={ExistingPublication}",
            languageCode, categoryName ?? "all", existingPublicationCode ?? "none");

        if (!string.IsNullOrWhiteSpace(existingPublicationCode))
        {
            logger.Debug("BiblePublicationCascadeHandler: Using existing publication={PublicationCode} from schedule",
                existingPublicationCode);
            
            // Verify the publication exists and can be queried with the selected language
            using (var verifyScope = scopeFactory.CreateScope())
            {
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
                var verifyNormalizedLanguageCode = languageCode.ToUpperInvariant();
                
                var canQueryWithLanguage = await verifyDb.BiblePublications
                    .AsNoTracking()
                    .AnyAsync(bp => bp.PublicationCode == existingPublicationCode && 
                                   bp.LanguageId != null &&
                                   bp.Language != null &&
                                   bp.Language.LanguageCode == verifyNormalizedLanguageCode);
                
                if (canQueryWithLanguage)
                {
                    var wasHarvested = await languageContentService.EnsurePublicationExistsAsync(existingPublicationCode, languageCode);
                    if (!wasHarvested)
                    {
                        logger.Warning("BiblePublicationCascadeHandler: Failed to harvest existing publication={PublicationCode} for language={LanguageCode}", 
                            existingPublicationCode, languageCode);
                        // Fall through to select a new publication
                    }
                    else
                    {
                        // Invalidate cache after downloading to ensure selectability checks use fresh data
                        mediaService.InvalidateBiblePublicationsCache(languageCode, categoryName);
                        // Harvest succeeded - proceed to get section and track
                        var languageModel = new LanguageListViewItemModel(new Language
                        {
                            LanguageCode = languageCode,
                            Name = currentSchedule.BiblePublicationLanguageName ?? languageCode,
                            Direction = currentSchedule.BiblePublicationLanguageDirection ?? "ltr"
                        });

                        var publicationModel = new PublicationListViewItemModel(new Publication
                        {
                            PublicationCode = existingPublicationCode,
                            Name = currentSchedule.BiblePublicationName ?? existingPublicationCode
                        });

                        var (resultSectionCode, resultTrackCode, resultSectionName, resultTrackTitle) =
                            await itemSelector.GetSectionAndTrackForPublicationAsync(publicationModel, languageModel);

                        if (string.IsNullOrWhiteSpace(resultTrackCode))
                        {
                            logger.Warning("BiblePublicationCascadeHandler: No valid track found for existing publication={PublicationCode} after harvesting", existingPublicationCode);
                            // Fall through to select a new publication
                        }
                        else
                        {
                            var existingPublicationName = currentSchedule.BiblePublicationName ?? existingPublicationCode;
                            var sectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(verifyDb, existingPublicationCode, languageCode);
                            UpdateSchedule(
                                currentSchedule,
                                existingPublicationCode,
                                existingPublicationName,
                                resultSectionCode,
                                resultSectionName,
                                resultTrackCode,
                                resultTrackTitle,
                                publicationModalItemCount,
                                sectionModalItemCount,
                                dispatcher);
                            return;
                        }
                    }
                }
                else
                {
                    logger.Debug("BiblePublicationCascadeHandler: Existing publication={PublicationCode} cannot be queried with language={LanguageCode}, will select new publication",
                        existingPublicationCode, languageCode);
                }
            }
        }

        // Get first publication - try publications with LanguageId for selected language, then publications without LanguageId
        // Publications list will show both publications for selected language + null language ID
        string? publicationCode = null;
        bool publicationWithoutLanguage = false;
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
        
        var query = db.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode);
        
        // Filter by category if provided (try current category first)
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            query = query.Where(pl => pl.Category != null && pl.Category.CategoryName == categoryName);
        }
        
        // Get publications, then sort by priority (nwt first, then bi12, then others)
        var publicationLanguages = await query
            .ToListAsync();
        
        // Sort by priority: nwt first, then bi12, then others, then by ID as tiebreaker
        publicationLanguages = publicationLanguages
            .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.PublicationCodeComparer)
            .ThenBy(pl => pl.Id)
            .ToList();

        // Cascade must fetch MINIMUM data:
        // - harvest ONLY the first viable publication (first section + tracks for first section)
        // - never ensure ALL publications or ALL sections here
        foreach (var pl in publicationLanguages)
        {
            // For dramas, use case-sensitive publication codes in DB ("Dramas"/"DramaticBibleReadings").
            // For others, preserve exact case from discovery (usually lower-case codes).
            var lowerCode = pl.PublicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            var publicationCodeForDb = isDrama
                ? (lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings")
                : pl.PublicationCode;

            var isHarvested = await languageContentService.EnsurePublicationExistsAsync(pl.PublicationCode, languageCode);
            if (!isHarvested)
            {
                logger.Debug(
                    "BiblePublicationCascadeHandler: Failed to harvest publication={PublicationCode} for language={LanguageCode}, trying next",
                    pl.PublicationCode,
                    languageCode);
                continue;
            }

            // Invalidate cache after downloading to ensure selectability checks use fresh data
            mediaService.InvalidateBiblePublicationsCache(languageCode, categoryName);

            var canQueryWithLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                                bp.LanguageId != null &&
                                bp.Language != null &&
                                bp.Language.LanguageCode == normalizedLanguageCode);

            if (!canQueryWithLanguage)
            {
                logger.Debug(
                    "BiblePublicationCascadeHandler: Publication={PublicationCode} harvested but cannot be queried with language={LanguageCode} (may not have LanguageId), trying next",
                    pl.PublicationCode,
                    languageCode);
                continue;
            }

            publicationCode = publicationCodeForDb;
            publicationWithoutLanguage = false;
            logger.Debug(
                "BiblePublicationCascadeHandler: Selected publication={PublicationCode} (harvested and queryable for language={LanguageCode})",
                publicationCode,
                languageCode);
            break;
        }
        
        if (string.IsNullOrEmpty(publicationCode))
        {
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var pubWithoutLanguage = await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp => bp.Category != null && 
                                bp.Category.CategoryName == categoryName &&
                                bp.LanguageId == null)
                    .OrderBy(bp => bp.Id)
                    .FirstOrDefaultAsync();
                
                if (pubWithoutLanguage != null)
                {
                    publicationCode = pubWithoutLanguage.PublicationCode;
                    publicationWithoutLanguage = true;
                    logger.Debug("BiblePublicationCascadeHandler: Selected publication without LanguageId={PublicationCode}",
                        publicationCode);
                }
            }
        }
        
        if (string.IsNullOrEmpty(publicationCode))
        {
            logger.Warning("BiblePublicationCascadeHandler: No publication found for language={LanguageCode}, category={CategoryName}",
                languageCode, categoryName ?? "all");
            return;
        }
        // Publication with LanguageId was harvested above (or already existed).

        // Get section and track
        string? sectionCode = null;
        string? trackCode = null;
        string sectionName = string.Empty;
        string publicationName = string.Empty;
        string trackTitle = string.Empty;

        if (publicationWithoutLanguage)
        {
            // For publications without LanguageId, query directly without a language
            // Get publication name from database
            using (var nameScope = scopeFactory.CreateScope())
            {
                var nameDb = nameScope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
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
                    var trackDb = trackScope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
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
                            var firstTrack = section.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).First();
                            trackCode = TrackCodeHelper.GetFromTrack(firstTrack);
                            trackTitle = firstTrack.Title ?? string.Empty;
                        }
                    }
                }
            }
            else
            {
                // Non-sectioned publication - get first track directly
                using (var trackScope = scopeFactory.CreateScope())
                {
                    var trackDb = trackScope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
                    var pub = await trackDb.BiblePublications
                        .AsNoTracking()
                        .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                        .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                        .FirstOrDefaultAsync();
                    
                    if (pub?.Tracks != null && pub.Tracks.Count > 0)
                    {
                        var firstTrack = pub.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).First();
                        trackCode = TrackCodeHelper.GetFromTrack(firstTrack);
                        trackTitle = firstTrack.Title ?? string.Empty;
                    }
                }
            }
        }
        else
        {
            // For publications with LanguageId, use the existing publication code from schedule
            var languageModel = new LanguageListViewItemModel(new Language
            {
                LanguageCode = languageCode,
                Name = currentSchedule.BiblePublicationLanguageName ?? languageCode,
                Direction = currentSchedule.BiblePublicationLanguageDirection ?? "ltr"
            });

            var publicationModel = new PublicationListViewItemModel(new Publication
            {
                PublicationCode = publicationCode,
                Name = currentSchedule.BiblePublicationName ?? publicationCode
            });

            var (resultSectionCode, resultTrackCode, resultSectionName, resultTrackTitle) =
                await itemSelector.GetSectionAndTrackForPublicationAsync(publicationModel, languageModel);

            if (string.IsNullOrWhiteSpace(resultTrackCode))
            {
                logger.Warning("BiblePublicationCascadeHandler: No valid track found for publication={PublicationCode}", publicationCode);
                return;
            }

            sectionCode = resultSectionCode;
            trackCode = resultTrackCode;
            sectionName = resultSectionName;
            trackTitle = resultTrackTitle;
            
            if (string.IsNullOrEmpty(publicationName))
            {
                publicationName = currentSchedule.BiblePublicationName ?? publicationCode;
            }
        }

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            logger.Warning("BiblePublicationCascadeHandler: No valid track found for publication={PublicationCode}",
                publicationCode);
            return;
        }

        var sectionModalCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
        UpdateSchedule(
            currentSchedule,
            publicationCode,
            publicationName,
            sectionCode,
            sectionName,
            trackCode ?? string.Empty,
            trackTitle,
            publicationModalItemCount,
            sectionModalCount,
            dispatcher,
            publicationWithoutLanguage);
    }

    private async Task HandlePublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.BiblePublicationLanguageCode!;
        var publicationCode = currentSchedule.BiblePublicationCode!;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        var publicationModalItemCount = await GetBiblePublicationModalItemCountAsync(languageCode, categoryName);

        logger.Information("BiblePublicationCascadeHandler: Publication cascade - publication={PublicationCode}, language={LanguageCode}",
            publicationCode, languageCode);

        // Use existing selector logic to get section and track
        var languageModel = new LanguageListViewItemModel(new Language
        {
            LanguageCode = languageCode,
            Name = currentSchedule.BiblePublicationLanguageName ?? languageCode,
            Direction = currentSchedule.BiblePublicationLanguageDirection ?? "ltr"
        });

        // Create a minimal publication model for the selector
        var publication = new Publication
        {
            PublicationCode = publicationCode,
            Name = currentSchedule.BiblePublicationName ?? publicationCode
        };
        var publicationModel = new PublicationListViewItemModel(publication);

        var (selectedSectionCode, trackCode, sectionName, trackTitle) =
            await itemSelector.GetSectionAndTrackForPublicationAsync(publicationModel, languageModel);

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            logger.Warning("BiblePublicationCascadeHandler: No valid track found");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var sectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
        UpdateSchedule(
            currentSchedule,
            publicationCode,
            currentSchedule.BiblePublicationName,
            selectedSectionCode,
            sectionName,
            trackCode,
            trackTitle,
            publicationModalItemCount,
            sectionModalItemCount,
            dispatcher);
    }

    private async Task HandleSectionCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.BiblePublicationLanguageCode!;
        var publicationCode = currentSchedule.BiblePublicationCode!;
        var sectionCode = currentSchedule.BiblePublicationSectionCode;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        var publicationModalItemCount = await GetBiblePublicationModalItemCountAsync(languageCode, categoryName);

        logger.Information("BiblePublicationCascadeHandler: Section cascade - sectionCode={SectionCode}, publication={PublicationCode}",
            sectionCode ?? "(none)", publicationCode);

        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, normalizedSectionCode);
        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning("BiblePublicationCascadeHandler: No tracks found for sectionCode={SectionCode}", sectionCode ?? "(none)");
            return;
        }

        var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).First();
        var trackCodeStr = TrackCodeHelper.GetFromTrack(firstTrack);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var sectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
        UpdateSchedule(
            currentSchedule,
            publicationCode,
            currentSchedule.BiblePublicationName,
            sectionCode,
            currentSchedule.BiblePublicationSectionName ?? string.Empty,
            trackCodeStr,
            firstTrack.Title ?? string.Empty,
            publicationModalItemCount,
            sectionModalItemCount,
            dispatcher);
    }

    private void UpdateSchedule(
        ScheduleStateItem currentSchedule,
        string publicationCode,
        string? publicationName,
        string? sectionCode,
        string sectionName,
        string trackCode,
        string trackTitle,
        int? publicationModalItemCount,
        int? sectionModalItemCount,
        IDispatcher dispatcher,
        bool publicationWithoutLanguage = false)
    {
        // Check if values have actually changed to prevent cascade cycles
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
        var currentTrackCode = currentSchedule.BiblePublicationTrackCode;
        var currentTrackTitle = currentSchedule.BiblePublicationTrackTitle;
        var currentPublicationCode = currentSchedule.BiblePublicationCode;
        var currentPublicationModalItemCount = currentSchedule.BiblePublicationModalItemCount;
        var currentSectionModalItemCount = currentSchedule.BiblePublicationSectionModalItemCount;
        
        var publicationChanged = !string.Equals(currentPublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase);
        var sectionChanged = !string.Equals(currentSectionCode, normalizedSectionCode, StringComparison.OrdinalIgnoreCase);
        var trackChanged = currentTrackCode != trackCode;
        var publicationModalCountChanged = currentPublicationModalItemCount != publicationModalItemCount;
        var sectionModalCountChanged = currentSectionModalItemCount != sectionModalItemCount;
        
        // If all values are already set correctly, don't dispatch to prevent infinite loop
        if (currentSchedule.BiblePublicationCode == publicationCode &&
            string.Equals(currentSectionCode, normalizedSectionCode, StringComparison.OrdinalIgnoreCase) &&
            currentTrackCode == trackCode &&
            currentTrackTitle == trackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged)
        {
            // Values haven't changed, skip dispatch to prevent cascade cycle
            logger.Debug("BiblePublicationCascadeHandler: Values unchanged, skipping dispatch to prevent cycle. publication={PublicationCode}, sectionCode={SectionCode}, track={TrackCode}",
                publicationCode, normalizedSectionCode ?? "(none)", trackCode);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.BiblePublicationCode = publicationCode;
        // If the selection changed, do not preserve old display names from a different selection.
        if (publicationChanged)
        {
            updatedSchedule.BiblePublicationName = !string.IsNullOrWhiteSpace(publicationName)
                ? publicationName
                : publicationCode;
        }
        else if (!string.IsNullOrWhiteSpace(publicationName))
        {
            updatedSchedule.BiblePublicationName = publicationName;
        }
        updatedSchedule.BiblePublicationSectionCode = normalizedSectionCode;
        // If the selection changed, clear stale display names even if new names aren't available yet.
        if (publicationChanged || sectionChanged)
        {
            updatedSchedule.BiblePublicationSectionName = !string.IsNullOrWhiteSpace(sectionName)
                ? sectionName
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(sectionName))
        {
            updatedSchedule.BiblePublicationSectionName = sectionName;
        }
        updatedSchedule.BiblePublicationTrackCode = trackCode;
        if (publicationChanged || sectionChanged || trackChanged)
        {
            updatedSchedule.BiblePublicationTrackTitle = !string.IsNullOrWhiteSpace(trackTitle)
                ? trackTitle
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(trackTitle))
        {
            updatedSchedule.BiblePublicationTrackTitle = trackTitle;
        }

        updatedSchedule.BiblePublicationModalItemCount = publicationModalItemCount;
        updatedSchedule.BiblePublicationSectionModalItemCount = sectionModalItemCount;
        // Do NOT reset progress here. Progress reset is applied only on Save.
        
        // ALWAYS preserve category - category can ONLY be changed via CategorySelectionAction
        // DeepClone() preserves the category, but explicitly ensure it's not null/empty
        if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationCategoryName) && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCategoryName))
        {
            // Preserve existing category from current schedule
            updatedSchedule.BiblePublicationCategoryId = currentSchedule.BiblePublicationCategoryId;
            updatedSchedule.BiblePublicationCategoryName = currentSchedule.BiblePublicationCategoryName;
            logger.Debug("BiblePublicationCascadeHandler: Preserving category={CategoryName} from current schedule",
                currentSchedule.BiblePublicationCategoryName);
        }
        
        // Handle language based on whether a no-language publication was selected:
        // - If a no-language publication was selected, reset language to "E" (English default)
        //   This ensures cascade consistency: the language row shows "English" and publications modal
        //   will show English publications + non-languaged publications.
        // - Otherwise, preserve language (language can only be changed via CategorySelectionAction or explicit user selection)
        if (publicationWithoutLanguage)
        {
            updatedSchedule.BiblePublicationLanguageCode = AppConstants.Media.DefaultLanguageCode;
            updatedSchedule.BiblePublicationLanguageName = null;
            updatedSchedule.BiblePublicationLanguageDirection = "ltr";
            logger.Debug("BiblePublicationCascadeHandler: Setting language to English default for no-language publication={PublicationCode}",
                publicationCode);
        }
        else if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationLanguageCode) && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationLanguageCode))
        {
            updatedSchedule.BiblePublicationLanguageCode = currentSchedule.BiblePublicationLanguageCode;
            updatedSchedule.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
            updatedSchedule.BiblePublicationLanguageDirection = currentSchedule.BiblePublicationLanguageDirection;
            logger.Debug("BiblePublicationCascadeHandler: Preserving language={LanguageCode} from current schedule",
                currentSchedule.BiblePublicationLanguageCode);
        }

        // Set biblePublicationUpdated=false to prevent re-triggering the cascade effect
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    private async Task<int?> GetBiblePublicationModalItemCountAsync(string languageCode, string? categoryName)
    {
        try
        {
            var publicationCodes = await biblePublicationService.GetAvailablePublicationCodesAsync(languageCode, categoryName);
            return publicationCodes.Count;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "BiblePublicationCascadeHandler: Error getting publication modal item count. LanguageCode={LanguageCode}, CategoryName={CategoryName}",
                languageCode, categoryName ?? "all");
            return null;
        }
    }

    private static async Task<int?> GetBiblePublicationSectionModalItemCountAsync(MediaDbContext db, string publicationCode, string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode) || !PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return 0;
        }

        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

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

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync();
    }
}
