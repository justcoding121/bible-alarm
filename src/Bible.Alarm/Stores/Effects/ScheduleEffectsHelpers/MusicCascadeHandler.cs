#nullable enable
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles cascade auto-population for Music publication selections.
/// Cascade order: Language → Publication → Section → Track
/// Note: MusicType is no longer used. Publication type (languaged vs. non-languaged) is inferred from LanguageId.
/// </summary>
public sealed class MusicCascadeHandler
{
    private readonly IMediaService mediaService;
    private readonly ILanguageContentService languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    public MusicCascadeHandler(
        IMediaService mediaService,
        ILanguageContentService languageContentService,
        IState<ApplicationState> state,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        this.mediaService = mediaService;
        this.languageContentService = languageContentService;
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
                logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncCurrentScheduleNullExiting);
                return;
            }

            var publicationCode = currentSchedule.MusicPublicationCode;
            var sectionCode = currentSchedule.MusicSectionCode;
            var trackCode = currentSchedule.MusicTrackCode;

            logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncPublicationSectionTrackMusicEnabled,
                publicationCode ?? "null", sectionCode ?? "null", trackCode ?? "null", currentSchedule.MusicEnabled);

            var publicationHasSections =
                !string.IsNullOrWhiteSpace(publicationCode) &&
                PublicationTypeHelper.HasSectionStructure(publicationCode);

            if (string.IsNullOrWhiteSpace(publicationCode))
            {
                logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncNoPublicationCodeCallingLanguageCascade);
                await HandleLanguageCascadeAsync(currentSchedule, dispatcher);
                return;
            }

            // Cascade 2/3: Publication/Section/Track cascade
            //
            // IMPORTANT:
            // Many music publications (vocal/instrumental) are FLAT (no sections). For those, SectionCode is expected to be null,
            // and we must NOT treat "missing section" as an incomplete selection once a TrackCode is already chosen.
            await HandlePublicationSectionTrackChainAsync(currentSchedule, publicationCode, sectionCode, trackCode, publicationHasSections, dispatcher);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ErrorDuringCascade);
        }
    }

    private async Task HandlePublicationSectionTrackChainAsync(
        ScheduleStateItem currentSchedule,
        string publicationCode,
        string? sectionCode,
        string? trackCode,
        bool publicationHasSections,
        IDispatcher dispatcher)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return;
        }

        var trackMissing = string.IsNullOrWhiteSpace(trackCode);

        if (publicationHasSections)
        {
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncSectionedNoSectionCodeCallingPublicationCascade);
                await HandlePublicationCascadeAsync(currentSchedule, dispatcher);
                return;
            }

            if (trackMissing)
            {
                logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncSectionedNoTrackCodeCallingSectionCascade);
                await HandleSectionCascadeAsync(currentSchedule, dispatcher);
                return;
            }
        }
        else if (trackMissing)
        {
            logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncFlatNoTrackCodeCallingFlatCascade);
            await HandleFlatPublicationCascadeAsync(currentSchedule, dispatcher);
            return;
        }

        logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncEverythingSetCallingRefreshModalCounts);
        await RefreshModalCountsIfNeededAsync(currentSchedule, dispatcher);
    }

    private async Task RefreshModalCountsIfNeededAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
            var sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, currentSchedule);

            logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.RefreshModalCountsIfNeededCurrentVsNew,
                currentSchedule.MusicPublicationModalItemCount, currentSchedule.MusicSectionModalItemCount,
                publicationModalItemCount, sectionModalItemCount);

            if (currentSchedule.MusicPublicationModalItemCount != publicationModalItemCount ||
                currentSchedule.MusicSectionModalItemCount != sectionModalItemCount)
            {
                var updatedSchedule = currentSchedule.DeepClone();
                updatedSchedule.MusicPublicationModalItemCount = publicationModalItemCount;
                updatedSchedule.MusicSectionModalItemCount = sectionModalItemCount;

                logger.Information(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.RefreshingModalCounts,
                    publicationModalItemCount, sectionModalItemCount, currentSchedule.MusicPublicationCode);

                // Use musicUpdated: true to trigger modal counts effect. Cascade handler will exit early since everything is already set.
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
            }
            else
            {
                logger.Debug(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ModalCountsUnchangedSkippingDispatch);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ErrorRefreshingModalCounts);
        }
    }

    private async Task HandleFlatPublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode!;

        logger.Information(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.FlatPublicationCascade,
            publicationCode, languageCode);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Determine whether this publication is stored without a language FK (e.g. melody/music catalog).
        var publication = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.PublicationCode == publicationCode &&
                         bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic))
            .FirstOrDefaultAsync();

        if (publication == null)
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationNotFoundInDatabase, publicationCode);
            return;
        }

        var publicationWithoutLanguage = publication.LanguageId == null;

        // For flat publications, GetFirstSectionAndTrackAsync will return (null, "", firstTrackCode, firstTrackTitle)
        // and we intentionally keep MusicSectionCode null.
        var (_, _, trackCode, trackTitle) =
            await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(
                mediaService,
                languageCode,
                publicationCode,
                publicationWithoutLanguage);

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication, publicationCode);
            return;
        }

        // Align with Bible cascade: set modal counts so row badges match (music category).
        var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = 0; // Flat publication has no sections.

        MusicCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new MusicCascadeScheduleUpdater.Mutation(
                publicationCode,
                currentSchedule.MusicPublicationName,
                null,
                string.Empty,
                trackCode,
                trackTitle,
                publicationModalItemCount,
                sectionModalItemCount),
            dispatcher);
    }

    private async Task HandleLanguageCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;

        logger.Information(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.LanguageCascade,
            languageCode);

        string? publicationCode = null;
        string? publicationName = null;
        var publicationWithoutLanguage = false;
        var needCatalog = false;

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            (publicationCode, publicationName, publicationWithoutLanguage, needCatalog) =
                await ResolveMusicPublicationFromLanguageDbAsync(db, languageCode);
        }

        if (needCatalog && !string.IsNullOrEmpty(publicationCode))
        {
            if (!await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode))
            {
                logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.FailedToCatalogPublication, publicationCode);
                return;
            }
            mediaService.InvalidateBiblePublicationsCache(languageCode, AppConstants.Media.BiblePublicationCategoryMusic);
            using (var scope2 = scopeFactory.CreateScope())
            {
                var db2 = scope2.ServiceProvider.GetRequiredService<MediaDbContext>();
                var normalizedLanguageCode = languageCode.ToUpperInvariant();
                var publication = await db2.BiblePublications
                    .AsNoTracking()
                    .Where(bp => bp.PublicationCode == publicationCode &&
                               bp.LanguageId != null &&
                               bp.Language != null &&
                               bp.Language.LanguageCode == normalizedLanguageCode)
                    .FirstOrDefaultAsync();
                publicationName = publication?.Name ?? publicationCode;
            }
        }

        if (string.IsNullOrEmpty(publicationCode))
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoPublicationFoundForLanguage,
                languageCode);
            return;
        }

        var (sectionCode, sectionName, trackCode, trackTitle) =
            await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(
                mediaService,
                languageCode,
                publicationCode,
                publicationWithoutLanguage);

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication,
                publicationCode);
            return;
        }

        int? publicationModalItemCount;
        int? sectionModalItemCount;
        using (var scopeCounts = scopeFactory.CreateScope())
        {
            var dbCounts = scopeCounts.ServiceProvider.GetRequiredService<MediaDbContext>();
            var tempSchedule = currentSchedule.DeepClone();
            tempSchedule.MusicPublicationCode = publicationCode;
            tempSchedule.MusicLanguageCode = publicationWithoutLanguage
                ? (currentSchedule.BiblePublicationLanguageCode ?? AppConstants.Media.DefaultLanguageCode)
                : languageCode;
            publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(dbCounts, tempSchedule);
            sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(dbCounts, tempSchedule);
        }

        // If using a no-language publication (e.g. iam), preserve existing music display language only.
        if (publicationWithoutLanguage)
        {
            var scheduleLanguageCode = currentSchedule.MusicLanguageCode ?? AppConstants.Media.DefaultLanguageCode;
            var updatedSchedule = currentSchedule.DeepClone();
            updatedSchedule.MusicLanguageCode = scheduleLanguageCode;
            updatedSchedule.MusicLanguageName = currentSchedule.MusicLanguageName;
            updatedSchedule.MusicPublicationCode = publicationCode;
            updatedSchedule.MusicPublicationName = publicationName;
            updatedSchedule.MusicSectionCode = sectionCode;
            updatedSchedule.MusicSectionName = !string.IsNullOrWhiteSpace(sectionName) ? sectionName : null;
            updatedSchedule.MusicTrackCode = trackCode;
            updatedSchedule.MusicTrackName = !string.IsNullOrWhiteSpace(trackTitle) ? trackTitle : null;
            updatedSchedule.MusicPublicationModalItemCount = publicationModalItemCount;
            updatedSchedule.MusicSectionModalItemCount = sectionModalItemCount;
            
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
            return;
        }

        MusicCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new MusicCascadeScheduleUpdater.Mutation(
                publicationCode,
                publicationName,
                sectionCode,
                sectionName,
                trackCode,
                trackTitle,
                publicationModalItemCount,
                sectionModalItemCount),
            dispatcher);
    }

    private static async Task<(string? PublicationCode, string? PublicationName, bool PublicationWithoutLanguage, bool NeedCatalog)>
        ResolveMusicPublicationFromLanguageDbAsync(MediaDbContext db, string languageCode)
    {
        if (!string.IsNullOrEmpty(languageCode))
            return await ResolveMusicPublicationWhenLanguageCodeSpecifiedAsync(db, languageCode).ConfigureAwait(false);

        return await ResolveMusicPublicationWhenLanguageCodeEmptyAsync(db).ConfigureAwait(false);
    }

    private static async Task<(string? PublicationCode, string? PublicationName, bool PublicationWithoutLanguage, bool NeedCatalog)>
        ResolveMusicPublicationWhenLanguageCodeSpecifiedAsync(MediaDbContext db, string languageCode)
    {
        string? publicationCode = null;
        string? publicationName = null;
        var publicationWithoutLanguage = false;
        var needCatalog = false;

        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var musicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryMusic);
        var publicationLanguage = (await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null &&
                            pl.Language.LanguageCode == normalizedLanguageCode &&
                            pl.Category != null &&
                            pl.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic)
                .ToListAsync())
            .OrderBy(pl => pl.PublicationCode, musicComparer)
            .ThenBy(pl => pl.Id)
            .FirstOrDefault();

        if (publicationLanguage != null)
        {
            publicationCode = publicationLanguage.PublicationCode;

            var publication = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.PublicationCode == publicationCode &&
                            bp.LanguageId != null &&
                            bp.Language != null &&
                            bp.Language.LanguageCode == normalizedLanguageCode)
                .FirstOrDefaultAsync();

            if (publication != null)
                publicationName = publication.Name;
            else
                needCatalog = true;
        }
        else
        {
            var noLangMusicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryMusic);
            var noLangPublication = (await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic) &&
                                bp.LanguageId == null)
                    .ToListAsync())
                .OrderBy(bp => bp.PublicationCode, noLangMusicComparer)
                .ThenBy(bp => bp.Id)
                .FirstOrDefault();

            if (noLangPublication != null)
            {
                publicationCode = noLangPublication.PublicationCode;
                publicationName = noLangPublication.Name;
                publicationWithoutLanguage = true;
            }
        }

        return (publicationCode, publicationName, publicationWithoutLanguage, needCatalog);
    }

    private static async Task<(string? PublicationCode, string? PublicationName, bool PublicationWithoutLanguage, bool NeedCatalog)>
        ResolveMusicPublicationWhenLanguageCodeEmptyAsync(MediaDbContext db)
    {
        string? publicationCode = null;
        string? publicationName = null;
        var publicationWithoutLanguage = false;
        var noLangMusicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryMusic);
        var publication = (await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic) &&
                            bp.LanguageId == null)
                .ToListAsync())
            .OrderBy(bp => bp.PublicationCode, noLangMusicComparer)
            .ThenBy(bp => bp.Id)
            .FirstOrDefault();

        if (publication != null)
        {
            publicationCode = publication.PublicationCode;
            publicationName = publication.Name;
            publicationWithoutLanguage = true;
        }

        return (publicationCode, publicationName, publicationWithoutLanguage, false);
    }

    private async Task HandlePublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode!;

        logger.Information(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationCascade,
            publicationCode, languageCode);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var publication = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Sections)
            .Where(bp => bp.PublicationCode == publicationCode &&
                       bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic))
            .FirstOrDefaultAsync();

        if (publication == null)
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationNotFoundInDatabase, publicationCode);
            return;
        }

        bool publicationWithoutLanguage = publication.LanguageId == null;

        var (sectionCode, sectionName, trackCode, trackTitle) =
            await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(
                mediaService,
                languageCode,
                publicationCode,
                publicationWithoutLanguage);

        if (string.IsNullOrWhiteSpace(trackCode))
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication,
                publicationCode);
            return;
        }

        // Align with Bible cascade: set modal counts (music category).
        var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, currentSchedule);

        MusicCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new MusicCascadeScheduleUpdater.Mutation(
                publicationCode,
                publication.Name,
                sectionCode,
                sectionName,
                trackCode,
                trackTitle,
                publicationModalItemCount,
                sectionModalItemCount),
            dispatcher);
    }

    private async Task HandleSectionCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode!;
        var sectionCode = currentSchedule.MusicSectionCode!;

        logger.Information(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.SectionCascade,
            sectionCode, publicationCode);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        var publication = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.PublicationCode == publicationCode &&
                       bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic))
            .FirstOrDefaultAsync();

        if (publication == null)
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationNotFound, publicationCode);
            return;
        }

        SortedDictionary<string, BiblePublicationTrack>? tracks = null;

        if (publication.LanguageId == null)
        {
            tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCode);
        }
        else
        {
            tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
        }

        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning(AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoTracksFoundForSection, sectionCode);
            return;
        }

        var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
        var sectionName = currentSchedule.MusicSectionName ?? string.Empty;
        var trackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(firstTrack);

        // Align with Bible cascade: set modal counts (music category).
        var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, currentSchedule);

        MusicCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            new MusicCascadeScheduleUpdater.Mutation(
                publicationCode,
                currentSchedule.MusicPublicationName,
                sectionCode,
                sectionName,
                trackCode,
                firstTrack.Title ?? string.Empty,
                publicationModalItemCount,
                sectionModalItemCount),
            dispatcher);
    }
}
