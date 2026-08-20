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

        var (publicationCode, publicationWithoutLanguage) =
            await ResolvePublicationCodeForLanguageCascadeAsync(languageCode, categoryName).ConfigureAwait(false);

        if (string.IsNullOrEmpty(publicationCode))
        {
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoPublicationFoundForLanguageCategory,
                languageCode, categoryName ?? "all");
            return;
        }
        // Publication with LanguageId was cataloged above (or already existed).

        // Get section and track
        var resolved = await TryResolveSectionAndTrackForLanguageCascadeAsync(
            currentSchedule,
            languageCode,
            publicationCode,
            publicationWithoutLanguage);

        if (resolved == null)
        {
            return;
        }

        var (sectionCode, trackCode, sectionName, publicationName, trackTitle) = resolved.Value;

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            logger.Warning(AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication,
                publicationCode);
            return;
        }

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var sectionModalCount = await GetBiblePublicationSectionModalItemCountAsync(db, publicationCode, languageCode);
            var tracksForCount = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, SectionCodeHelper.Normalize(sectionCode));
            var trackModalItemCount = tracksForCount?.Count;
            BiblePublicationCascadeScheduleUpdater.UpdateSchedule(
                logger,
                currentSchedule,
                new BiblePublicationCascadeScheduleUpdater.Mutation(
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
    }

    private async Task<(string? PublicationCode, bool PublicationWithoutLanguage)> ResolvePublicationCodeForLanguageCascadeAsync(
        string languageCode,
        string? categoryName)
    {
        string? publicationCode = null;
        var publicationWithoutLanguage = false;
        var normalizedLanguageCode = languageCode.ToUpperInvariant();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

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

        publicationLanguages = publicationLanguages
            .OrderBy(pl => pl.PublicationCode, PublicationCodeHelper.GetPublicationCodeComparerForCategory(categoryName))
            .ThenBy(pl => pl.Id)
            .ToList();

        var pickedPublication = await TryPickFirstQueryablePublicationAfterLanguageCascadeAsync(
            publicationLanguages,
            languageCode,
            normalizedLanguageCode,
            categoryName,
            db).ConfigureAwait(false);
        if (pickedPublication.HasValue)
        {
            publicationCode = pickedPublication.Value.PublicationCode;
            publicationWithoutLanguage = pickedPublication.Value.PublicationWithoutLanguage;
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

        return (publicationCode, publicationWithoutLanguage);
    }

    private async Task<(string? SectionCode, string? TrackCode, string SectionName, string PublicationName, string TrackTitle)?>
        TryResolveSectionAndTrackForLanguageCascadeAsync(
            ScheduleStateItem currentSchedule,
            string languageCode,
            string publicationCode,
            bool publicationWithoutLanguage)
    {
        if (publicationWithoutLanguage)
        {
            var nl = await BiblePublicationCascadeNoLanguageResolver.GetFirstSectionAndTrackAsync(
                mediaService,
                scopeFactory,
                publicationCode);
            return (nl.sectionCode, nl.trackCode, nl.sectionName, nl.publicationName, nl.trackTitle);
        }

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
            logger.Warning(
                AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication,
                publicationCode);
            return null;
        }

        var resolvedPublicationName = currentSchedule.BiblePublicationName ?? publicationCode;
        return (resultSectionCode, resultTrackCode, resultSectionName, resolvedPublicationName, resultTrackTitle);
    }

    private async Task<(string PublicationCode, bool PublicationWithoutLanguage)?>
        TryPickFirstQueryablePublicationAfterLanguageCascadeAsync(
            List<PublicationLanguage> publicationLanguages,
            string languageCode,
            string normalizedLanguageCode,
            string? categoryName,
            MediaDbContext db)
    {
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

            logger.Debug(
                AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SelectedPublicationCatalogedQueryableForLanguage,
                publicationCodeForDb,
                languageCode);
            return (publicationCodeForDb, false);
        }

        return null;
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
            new BiblePublicationCascadeScheduleUpdater.Mutation(
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
            new BiblePublicationCascadeScheduleUpdater.Mutation(
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
            new BiblePublicationCascadeScheduleUpdater.Mutation(
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
