#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;
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
            logger.Error(ex, AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ErrorDuringCascade);
        }
    }

    private async Task HandleLanguageCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.BiblePublicationLanguageCode!;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        var existingPublicationCode = currentSchedule.BiblePublicationCode;
        var publicationModalItemCount = await GetBiblePublicationModalItemCountAsync(languageCode, categoryName);

        logger.Information(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.LanguageCascade,
            languageCode, categoryName ?? "all", existingPublicationCode ?? "none");

        if (await TryApplyExistingPublicationCascadeAsync(
                currentSchedule,
                languageCode,
                categoryName,
                existingPublicationCode,
                publicationModalItemCount,
                dispatcher))
        {
            return;
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
            query = query.Where(pl => pl.Category != null && pl.Category.CategoryCode == categoryName);
        }
        
        // Get publications, then sort by priority (nwt first, then bi12, then others)
        var publicationLanguages = await query
            .ToListAsync();
        
        // Sort by category-specific priority (Bible: nwt first; Magazine: latest year first; etc.)
        publicationLanguages = publicationLanguages
            .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.GetPublicationCodeComparerForCategory(categoryName))
            .ThenBy(pl => pl.Id)
            .ToList();

        // Cascade must fetch MINIMUM data:
        // - catalog ONLY the first viable publication (first section + tracks for first section)
        // - never ensure ALL publications or ALL sections here
        foreach (var (pl, publicationCodeForDb) in publicationLanguages.Select(pl =>
                     (pl, PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(pl.PublicationCode))))
        {
            var isCataloged = await languageContentService.EnsurePublicationExistsAsync(pl.PublicationCode, languageCode);
            if (!isCataloged)
            {
                logger.Debug(
                    AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.FailedToCatalogPublicationTryingNext,
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
                    AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PublicationCatalogedCannotQueryWithLanguageTryingNext,
                    pl.PublicationCode,
                    languageCode);
                continue;
            }

            publicationCode = publicationCodeForDb;
            publicationWithoutLanguage = false;
            logger.Debug(
                AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SelectedPublicationCatalogedQueryableForLanguage,
                publicationCode,
                languageCode);
            break;
        }
        
        if (string.IsNullOrEmpty(publicationCode) &&
            !string.IsNullOrWhiteSpace(categoryName) &&
            await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == categoryName) &&
                            bp.LanguageId == null)
                .OrderBy(bp => bp.Id)
                .FirstOrDefaultAsync() is { } pubWithoutLanguage)
        {
            publicationCode = pubWithoutLanguage.PublicationCode;
            publicationWithoutLanguage = true;
            logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SelectedPublicationWithoutLanguageId,
                publicationCode);
        }
        
        if (string.IsNullOrEmpty(publicationCode))
        {
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoPublicationFoundForLanguageCategory,
                languageCode, categoryName ?? "all");
            return;
        }
        // Publication with LanguageId was cataloged above (or already existed).

        // Get section and track
        string? sectionCode = null;
        string? trackCode = null;
        string sectionName = string.Empty;
        string publicationName = string.Empty;
        string trackTitle = string.Empty;

        if (publicationWithoutLanguage)
        {
            (sectionCode, trackCode, sectionName, trackTitle, publicationName) = await BiblePublicationCascadeNoLanguageResolver.GetFirstSectionAndTrackAsync(
                mediaService,
                scopeFactory,
                publicationCode);
        }
        else
        {
            // For publications with LanguageId, use the existing publication code from schedule
            var languageDisplayName = currentSchedule.BiblePublicationLanguageName ?? languageCode;
            var languageModel = new LanguageListViewItemModel(new Language
            {
                LanguageCode = languageCode,
                Direction = currentSchedule.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight
            }, languageDisplayName);

            var publicationModel = new PublicationListViewItemModel(new Publication
            {
                PublicationCode = publicationCode,
                Name = currentSchedule.BiblePublicationName ?? publicationCode
            });

            var (resultSectionCode, resultTrackCode, resultSectionName, resultTrackTitle) =
                await itemSelector.GetSectionAndTrackForPublicationAsync(publicationModel, languageModel);

            if (string.IsNullOrWhiteSpace(resultTrackCode))
            {
                logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication, publicationCode);
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
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication,
                publicationCode);
            return;
        }

        var sectionModalCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
        var tracksForCount = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, SectionCodeHelper.Normalize(sectionCode));
        var trackModalItemCount = tracksForCount?.Count;
        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new BiblePublicationCascadeScheduleMutation(
                publicationCode,
                publicationName,
                sectionCode,
                sectionName,
                trackCode,
                trackTitle,
                publicationModalItemCount,
                sectionModalCount,
                trackModalItemCount,
                publicationWithoutLanguage),
            dispatcher);
    }

    private async Task<bool> TryApplyExistingPublicationCascadeAsync(
        ScheduleStateItem currentSchedule,
        string languageCode,
        string? categoryName,
        string? existingPublicationCode,
        int? publicationModalItemCount,
        IDispatcher dispatcher)
    {
        if (string.IsNullOrWhiteSpace(existingPublicationCode))
        {
            return false;
        }

        logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.UsingExistingPublicationFromSchedule,
            existingPublicationCode);

        var wasCataloged = await languageContentService.EnsurePublicationExistsAsync(existingPublicationCode, languageCode);
        if (!wasCataloged)
        {
            logger.Debug(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ExistingPublicationNotAvailableSelectingNew,
                existingPublicationCode, languageCode);
            return false;
        }

        mediaService.InvalidateBiblePublicationsCache(languageCode, categoryName);

        var languageDisplayName = currentSchedule.BiblePublicationLanguageName ?? languageCode;
        var languageModel = new LanguageListViewItemModel(new Language
        {
            LanguageCode = languageCode,
            Direction = currentSchedule.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight
        }, languageDisplayName);

        var publicationModel = new PublicationListViewItemModel(new Publication
        {
            PublicationCode = existingPublicationCode,
            Name = currentSchedule.BiblePublicationName ?? existingPublicationCode
        });

        var (resultSectionCode, resultTrackCode, resultSectionName, resultTrackTitle) =
            await itemSelector.GetSectionAndTrackForPublicationAsync(publicationModel, languageModel);

        if (string.IsNullOrWhiteSpace(resultTrackCode))
        {
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForExistingPublicationAfterCataloging,
                existingPublicationCode);
            return false;
        }

        var existingPublicationName = currentSchedule.BiblePublicationName ?? existingPublicationCode;
        using var verifyScope = scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Database.MediaDbContext>();
        var sectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(verifyDb, existingPublicationCode, languageCode);
        var existingTracks = await mediaService.GetBiblePublicationTracks(languageCode, existingPublicationCode, SectionCodeHelper.Normalize(resultSectionCode));
        var existingTrackCount = existingTracks?.Count;
        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new BiblePublicationCascadeScheduleMutation(
                existingPublicationCode,
                existingPublicationName,
                resultSectionCode,
                resultSectionName,
                resultTrackCode,
                resultTrackTitle,
                publicationModalItemCount,
                sectionModalItemCount,
                existingTrackCount),
            dispatcher);
        return true;
    }

    private async Task HandlePublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.BiblePublicationLanguageCode!;
        var publicationCode = currentSchedule.BiblePublicationCode!;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        var publicationModalItemCount = await GetBiblePublicationModalItemCountAsync(languageCode, categoryName);

        logger.Information(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PublicationCascade,
            publicationCode, languageCode);

        // Use existing selector logic to get section and track
        var languageDisplayName = currentSchedule.BiblePublicationLanguageName ?? languageCode;
        var languageModel = new LanguageListViewItemModel(new Language
        {
            LanguageCode = languageCode,
            Direction = currentSchedule.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight
        }, languageDisplayName);

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
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFound);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var sectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
        var tracksForCount = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, SectionCodeHelper.Normalize(selectedSectionCode));
        var trackModalItemCount = tracksForCount?.Count;
        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new BiblePublicationCascadeScheduleMutation(
                publicationCode,
                currentSchedule.BiblePublicationName,
                selectedSectionCode,
                sectionName,
                trackCode,
                trackTitle,
                publicationModalItemCount,
                sectionModalItemCount,
                trackModalItemCount),
            dispatcher);
    }

    private async Task HandleSectionCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.BiblePublicationLanguageCode!;
        var publicationCode = currentSchedule.BiblePublicationCode!;
        var sectionCode = currentSchedule.BiblePublicationSectionCode;
        var categoryName = currentSchedule.BiblePublicationCategoryName;
        var publicationModalItemCount = await GetBiblePublicationModalItemCountAsync(languageCode, categoryName);

        logger.Information(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SectionCascade,
            sectionCode ?? "(none)", publicationCode);

        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, normalizedSectionCode);
        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoTracksFoundForSectionCode, sectionCode ?? "(none)");
            return;
        }

        var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
        var trackCodeStr = TrackCodeHelper.GetFromTrack(firstTrack);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var sectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
        var trackModalItemCount = tracks.Count;
        BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new BiblePublicationCascadeScheduleMutation(
                publicationCode,
                currentSchedule.BiblePublicationName,
                sectionCode,
                currentSchedule.BiblePublicationSectionName ?? string.Empty,
                trackCodeStr,
                firstTrack.Title ?? string.Empty,
                publicationModalItemCount,
                sectionModalItemCount,
                trackModalItemCount),
            dispatcher);
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
            logger.Error(ex, AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ErrorGettingPublicationModalItemCount,
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
