#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Common.Extensions;
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
    private readonly IBiblePublicationService biblePublicationService;
    private readonly IMediaService mediaService;
    private readonly ILanguageContentService languageContentService;
    private readonly IState<ApplicationState> state;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    public MusicCascadeHandler(
        IBiblePublicationService biblePublicationService,
        IMediaService mediaService,
        ILanguageContentService languageContentService,
        IState<ApplicationState> state,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        this.biblePublicationService = biblePublicationService;
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
                logger.Debug("MusicCascadeHandler: HandleAsync - CurrentSchedule is null, exiting");
                return;
            }

            var languageCode = currentSchedule.MusicLanguageCode;
            var publicationCode = currentSchedule.MusicPublicationCode;
            var sectionCode = currentSchedule.MusicSectionCode;
            var trackCode = currentSchedule.MusicTrackCode;

            logger.Debug("MusicCascadeHandler: HandleAsync - PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}, MusicEnabled={MusicEnabled}",
                publicationCode ?? "null", sectionCode ?? "null", trackCode ?? "null", currentSchedule.MusicEnabled);

            var publicationHasSections =
                !string.IsNullOrWhiteSpace(publicationCode) &&
                PublicationTypeHelper.HasSectionStructure(publicationCode);

            // Cascade 1: Publication not selected → populate publication, section, track
            if (string.IsNullOrWhiteSpace(publicationCode))
            {
                logger.Debug("MusicCascadeHandler: HandleAsync - No publication code, calling HandleLanguageCascadeAsync");
                await HandleLanguageCascadeAsync(currentSchedule, dispatcher);
                return;
            }

            // Cascade 2/3: Publication/Section/Track cascade
            //
            // IMPORTANT:
            // Many music publications (vocal/instrumental) are FLAT (no sections). For those, SectionCode is expected to be null,
            // and we must NOT treat "missing section" as an incomplete selection once a TrackCode is already chosen.
            if (!string.IsNullOrWhiteSpace(publicationCode))
            {
                var trackMissing = string.IsNullOrWhiteSpace(trackCode);

                // Sectioned publications (e.g. "iam") need a section first.
                if (publicationHasSections)
                {
                    if (string.IsNullOrWhiteSpace(sectionCode))
                    {
                        logger.Debug("MusicCascadeHandler: HandleAsync - Sectioned publication but no section code, calling HandlePublicationCascadeAsync");
                        await HandlePublicationCascadeAsync(currentSchedule, dispatcher);
                        return;
                    }

                    if (trackMissing)
                    {
                        logger.Debug("MusicCascadeHandler: HandleAsync - Sectioned publication but no track code, calling HandleSectionCascadeAsync");
                        await HandleSectionCascadeAsync(currentSchedule, dispatcher);
                        return;
                    }
                }
                else
                {
                    // Flat publications: only cascade when track is missing.
                    if (trackMissing)
                    {
                        logger.Debug("MusicCascadeHandler: HandleAsync - Flat publication but no track code, calling HandleFlatPublicationCascadeAsync");
                        await HandleFlatPublicationCascadeAsync(currentSchedule, dispatcher);
                        return;
                    }
                }

                // Everything is already set - ensure modal counts are refreshed (e.g., when music is enabled on existing schedule)
                // This ensures the section row arrow shows correctly when music is enabled
                logger.Debug("MusicCascadeHandler: HandleAsync - Everything is set, calling RefreshModalCountsIfNeededAsync");
                await RefreshModalCountsIfNeededAsync(currentSchedule, dispatcher);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MusicCascadeHandler: Error during cascade");
        }
    }

    private async Task RefreshModalCountsIfNeededAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
            var sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, currentSchedule);

            logger.Debug("MusicCascadeHandler: RefreshModalCountsIfNeeded - Current: PublicationCount={CurrentPubCount}, SectionCount={CurrentSectionCount}, New: PublicationCount={NewPubCount}, SectionCount={NewSectionCount}",
                currentSchedule.MusicPublicationModalItemCount, currentSchedule.MusicSectionModalItemCount,
                publicationModalItemCount, sectionModalItemCount);

            // Only dispatch if counts have changed or are missing
            if (currentSchedule.MusicPublicationModalItemCount != publicationModalItemCount ||
                currentSchedule.MusicSectionModalItemCount != sectionModalItemCount)
            {
                var updatedSchedule = currentSchedule.DeepClone();
                updatedSchedule.MusicPublicationModalItemCount = publicationModalItemCount;
                updatedSchedule.MusicSectionModalItemCount = sectionModalItemCount;

                logger.Information("MusicCascadeHandler: Refreshing modal counts. PublicationCount={PublicationCount}, SectionCount={SectionCount}, PublicationCode={PublicationCode}",
                    publicationModalItemCount, sectionModalItemCount, currentSchedule.MusicPublicationCode);

                // Use musicUpdated: true to trigger modal counts effect. Cascade handler will exit early since everything is already set.
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
            }
            else
            {
                logger.Debug("MusicCascadeHandler: Modal counts unchanged, skipping dispatch");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MusicCascadeHandler: Error refreshing modal counts");
        }
    }

    private async Task HandleFlatPublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode!;

        logger.Information("MusicCascadeHandler: Flat publication cascade - publication={PublicationCode}, language={LanguageCode}",
            publicationCode, languageCode);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Determine whether this publication is stored without a language FK (e.g. melody/music catalog).
        var publication = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.PublicationCode == publicationCode &&
                         bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == "Music"))
            .FirstOrDefaultAsync();

        if (publication == null)
        {
            logger.Warning("MusicCascadeHandler: Publication not found in database: {PublicationCode}", publicationCode);
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
            logger.Warning("MusicCascadeHandler: No valid track found for publication={PublicationCode}", publicationCode);
            return;
        }

        // Align with Bible cascade: set modal counts so row badges match (category = "Music").
        var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = 0; // Flat publication has no sections.

        MusicCascadeScheduleUpdater.UpdateSchedule(
            logger,
            currentSchedule,
            publicationCode,
            currentSchedule.MusicPublicationName,
            sectionCode: null,
            sectionName: string.Empty,
            trackCode: trackCode,
            trackTitle: trackTitle,
            publicationModalItemCount,
            sectionModalItemCount,
            dispatcher);
    }

    private async Task HandleLanguageCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;

        logger.Information("MusicCascadeHandler: Language cascade - language={LanguageCode}",
            languageCode);

        string? publicationCode = null;
        string? publicationName = null;
        bool publicationWithoutLanguage = false;
        string? effectiveLanguageCode = languageCode;
        bool needCatalog = false;

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            if (!string.IsNullOrEmpty(languageCode))
            {
                var normalizedLanguageCode = languageCode.ToUpperInvariant();
                var musicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory("Music");
                var publicationLanguage = (await db.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Language)
                    .Include(pl => pl.Category)
                    .Where(pl => pl.Language != null &&
                               pl.Language.LanguageCode == normalizedLanguageCode &&
                               pl.Category != null &&
                               pl.Category.CategoryCode == "Music")
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
                    {
                        publicationName = publication.Name;
                    }
                    else
                    {
                        needCatalog = true;
                    }
                }
                else
                {
                    var noLangMusicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory("Music");
                    var noLangPublication = (await db.BiblePublications
                        .AsNoTracking()
                        .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == "Music") &&
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
                        effectiveLanguageCode = null;
                    }
                }
            }
            else
            {
                var noLangMusicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory("Music");
                var publication = (await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == "Music") &&
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
            }
        }

        if (needCatalog && !string.IsNullOrEmpty(publicationCode))
        {
            if (!await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode))
            {
                logger.Warning("MusicCascadeHandler: Failed to catalog publication={PublicationCode}", publicationCode);
                return;
            }
            mediaService.InvalidateBiblePublicationsCache(languageCode, "Music");
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
            logger.Warning("MusicCascadeHandler: No publication found for language={LanguageCode}",
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
            logger.Warning("MusicCascadeHandler: No valid track found for publication={PublicationCode}",
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

        MusicCascadeScheduleUpdater.UpdateSchedule(logger, currentSchedule, publicationCode, publicationName, sectionCode, sectionName, trackCode, trackTitle, publicationModalItemCount, sectionModalItemCount, dispatcher);
    }

    private async Task HandlePublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode!;

        logger.Information("MusicCascadeHandler: Publication cascade - publication={PublicationCode}, language={LanguageCode}",
            publicationCode, languageCode);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Check if publication has LanguageId
        var publication = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Sections)
            .Where(bp => bp.PublicationCode == publicationCode &&
                       bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == "Music"))
            .FirstOrDefaultAsync();

        if (publication == null)
        {
            logger.Warning("MusicCascadeHandler: Publication not found in database: {PublicationCode}", publicationCode);
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
            logger.Warning("MusicCascadeHandler: No valid track found for publication={PublicationCode}",
                publicationCode);
            return;
        }

        // Align with Bible cascade: set modal counts (category = "Music").
        var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, currentSchedule);

        MusicCascadeScheduleUpdater.UpdateSchedule(logger, currentSchedule, publicationCode, publication.Name, sectionCode, sectionName, trackCode, trackTitle, publicationModalItemCount, sectionModalItemCount, dispatcher);
    }

    private async Task HandleSectionCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var publicationCode = currentSchedule.MusicPublicationCode!;
        var sectionCode = currentSchedule.MusicSectionCode!;

        logger.Information("MusicCascadeHandler: Section cascade - section={SectionCode}, publication={PublicationCode}",
            sectionCode, publicationCode);

        // Check if publication has LanguageId
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        var publication = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.PublicationCode == publicationCode &&
                       bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == "Music"))
            .FirstOrDefaultAsync();

        if (publication == null)
        {
            logger.Warning("MusicCascadeHandler: Publication not found: {PublicationCode}", publicationCode);
            return;
        }

        SortedDictionary<string, BiblePublicationTrack>? tracks = null;

        if (publication.LanguageId == null)
        {
            // Publication without language - use GetBiblePublicationTracks with empty language code
            tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCode);
        }
        else
        {
            // Publication with language
            tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
        }

        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning("MusicCascadeHandler: No tracks found for sectionCode={SectionCode}", sectionCode);
            return;
        }

        var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).First();
        var sectionName = currentSchedule.MusicSectionName ?? string.Empty;
        var trackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(firstTrack);

        // Align with Bible cascade: set modal counts (category = "Music").
        var publicationModalItemCount = await MusicCascadeModalCountHelper.GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = await MusicCascadeModalCountHelper.GetMusicSectionModalItemCountAsync(db, currentSchedule);

        MusicCascadeScheduleUpdater.UpdateSchedule(logger, currentSchedule, publicationCode, currentSchedule.MusicPublicationName, sectionCode, sectionName, trackCode, firstTrack.Title ?? string.Empty, publicationModalItemCount, sectionModalItemCount, dispatcher);
    }
}
