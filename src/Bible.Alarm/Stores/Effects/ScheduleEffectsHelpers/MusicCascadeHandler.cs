#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
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

            var publicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, currentSchedule);
            var sectionModalItemCount = await GetMusicSectionModalItemCountAsync(db, currentSchedule);

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
                         bp.Category != null &&
                         bp.Category.CategoryName == "Music")
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
        var publicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = 0; // Flat publication has no sections.

        UpdateSchedule(
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

        // Get first publication based on language selection
        string? publicationCode = null;
        string? publicationName = null;
        bool publicationWithoutLanguage = false;
        string? effectiveLanguageCode = languageCode;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        if (!string.IsNullOrEmpty(languageCode))
        {
            // Language is selected - get publications for that language AND non-languaged publications
            // Prefer languaged publications first, but if none found, use non-languaged
            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var publicationLanguage = await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null &&
                           pl.Language.LanguageCode == normalizedLanguageCode &&
                           pl.Category != null &&
                           pl.Category.CategoryName == "Music")
                .OrderBy(pl => pl.Id)
                .FirstOrDefaultAsync();

            if (publicationLanguage != null)
            {
                publicationCode = publicationLanguage.PublicationCode;
                
                // Check if publication exists in BiblePublications
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
                    // Publication doesn't exist yet - harvest it
                    if (!await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode))
                    {
                        logger.Warning("MusicCascadeHandler: Failed to harvest publication={PublicationCode}", publicationCode);
                        return;
                    }
                    
                    // Invalidate cache after downloading to ensure UI display/selectability checks use fresh data.
                    mediaService.InvalidateBiblePublicationsCache(languageCode, "Music");

                    // Re-query to get the actual publication
                    publication = await db.BiblePublications
                        .AsNoTracking()
                        .Where(bp => bp.PublicationCode == publicationCode &&
                                   bp.LanguageId != null &&
                                   bp.Language != null &&
                                   bp.Language.LanguageCode == normalizedLanguageCode)
                        .FirstOrDefaultAsync();
                    
                    publicationName = publication?.Name ?? publicationCode;
                }
            }
            else
            {
                // No languaged publication found for this language - fall back to non-languaged (like "iam")
                var noLangPublication = await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp => bp.Category != null &&
                               bp.Category.CategoryName == "Music" &&
                               bp.LanguageId == null)
                    .OrderBy(bp => bp.Id)
                    .FirstOrDefaultAsync();

                if (noLangPublication != null)
                {
                    publicationCode = noLangPublication.PublicationCode;
                    publicationName = noLangPublication.Name;
                    publicationWithoutLanguage = true;
                    // For non-languaged publications, we store null for LanguageCode
                    effectiveLanguageCode = null;
                }
            }
        }
        else
        {
            // No language selected - get first non-languaged publication (like "iam")
            var publication = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.Category != null &&
                           bp.Category.CategoryName == "Music" &&
                           bp.LanguageId == null)
                .OrderBy(bp => bp.Id)
                .FirstOrDefaultAsync();

            if (publication != null)
            {
                publicationCode = publication.PublicationCode;
                publicationName = publication.Name;
                publicationWithoutLanguage = true;
            }
        }

        if (string.IsNullOrEmpty(publicationCode))
        {
            logger.Warning("MusicCascadeHandler: No publication found for language={LanguageCode}",
                languageCode);
            return;
        }

        // Get section and track
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

        // Align with Bible cascade: set modal counts so row badges match (category = "Music").
        var tempSchedule = currentSchedule.DeepClone();
        tempSchedule.MusicPublicationCode = publicationCode;
        tempSchedule.MusicLanguageCode = publicationWithoutLanguage ? null : languageCode;
        var publicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, tempSchedule);
        var sectionModalItemCount = await GetMusicSectionModalItemCountAsync(db, tempSchedule);

        // If using a non-languaged publication, clear the language code from state
        if (publicationWithoutLanguage)
        {
            var updatedSchedule = currentSchedule.DeepClone();
            updatedSchedule.MusicLanguageCode = null;
            updatedSchedule.MusicLanguageName = null;
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

        UpdateSchedule(currentSchedule, publicationCode, publicationName, sectionCode, sectionName, trackCode, trackTitle, publicationModalItemCount, sectionModalItemCount, dispatcher);
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
                       bp.Category != null &&
                       bp.Category.CategoryName == "Music")
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
        var publicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = await GetMusicSectionModalItemCountAsync(db, currentSchedule);

        UpdateSchedule(currentSchedule, publicationCode, publication.Name, sectionCode, sectionName, trackCode, trackTitle, publicationModalItemCount, sectionModalItemCount, dispatcher);
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
                       bp.Category != null &&
                       bp.Category.CategoryName == "Music")
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
        var publicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, currentSchedule);
        var sectionModalItemCount = await GetMusicSectionModalItemCountAsync(db, currentSchedule);

        UpdateSchedule(currentSchedule, publicationCode, currentSchedule.MusicPublicationName, sectionCode, sectionName, trackCode, firstTrack.Title ?? string.Empty, publicationModalItemCount, sectionModalItemCount, dispatcher);
    }

    private void UpdateSchedule(ScheduleStateItem currentSchedule, string publicationCode, string? publicationName,
        string? sectionCode, string sectionName, string trackCode, string trackTitle,
        int? publicationModalItemCount, int? sectionModalItemCount, IDispatcher dispatcher)
    {
        // Check if values have actually changed to prevent cascade cycles (align with Bible cascade).
        var currentSectionCode = currentSchedule.MusicSectionCode;
        var currentTrackCode = currentSchedule.MusicTrackCode;
        var currentTrackTitle = currentSchedule.MusicTrackName;
        var currentPublicationCode = currentSchedule.MusicPublicationCode;
        var currentPublicationModalItemCount = currentSchedule.MusicPublicationModalItemCount;
        var currentSectionModalItemCount = currentSchedule.MusicSectionModalItemCount;

        var publicationChanged = !string.Equals(currentPublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase);
        var sectionChanged = !string.Equals(currentSectionCode, sectionCode, StringComparison.OrdinalIgnoreCase);
        var trackChanged = currentTrackCode != trackCode;
        var publicationModalCountChanged = currentPublicationModalItemCount != publicationModalItemCount;
        var sectionModalCountChanged = currentSectionModalItemCount != sectionModalItemCount;

        // If all values are already set correctly, don't dispatch to prevent infinite loop
        if (currentSchedule.MusicPublicationCode == publicationCode &&
            string.Equals(currentSectionCode, sectionCode, StringComparison.OrdinalIgnoreCase) &&
            currentTrackCode == trackCode &&
            currentTrackTitle == trackTitle &&
            !publicationModalCountChanged &&
            !sectionModalCountChanged)
        {
            logger.Debug("MusicCascadeHandler: Values unchanged, skipping dispatch to prevent cycle. publication={PublicationCode}, section={SectionCode}, track={TrackCode}",
                publicationCode, sectionCode ?? "null", trackCode);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.MusicPublicationCode = publicationCode;
        if (publicationChanged)
        {
            updatedSchedule.MusicPublicationName = !string.IsNullOrWhiteSpace(publicationName)
                ? publicationName
                : publicationCode;
        }
        else if (!string.IsNullOrWhiteSpace(publicationName))
        {
            updatedSchedule.MusicPublicationName = publicationName;
        }
        updatedSchedule.MusicSectionCode = sectionCode;
        if (publicationChanged || sectionChanged)
        {
            updatedSchedule.MusicSectionName = !string.IsNullOrWhiteSpace(sectionName)
                ? sectionName
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(sectionName))
        {
            updatedSchedule.MusicSectionName = sectionName;
        }
        updatedSchedule.MusicTrackCode = trackCode;
        if (publicationChanged || sectionChanged || trackChanged)
        {
            updatedSchedule.MusicTrackName = !string.IsNullOrWhiteSpace(trackTitle)
                ? trackTitle
                : null;
        }
        else if (!string.IsNullOrWhiteSpace(trackTitle))
        {
            updatedSchedule.MusicTrackName = trackTitle;
        }

        updatedSchedule.MusicPublicationModalItemCount = publicationModalItemCount;
        updatedSchedule.MusicSectionModalItemCount = sectionModalItemCount;

        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
    }

    /// <summary>
    /// Mirror of ScheduleEffects.GetMusicPublicationModalItemCountAsync.
    /// Category is always "Music"; when MusicLanguageCode is null, effective language is "E" (same as Bible row logic with category from schedule).
    /// </summary>
    private static async Task<int?> GetMusicPublicationModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryName == "Music");

        query = query.Where(pl =>
            (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
            pl.LanguageId == null);

        return await query
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .CountAsync();
    }

    /// <summary>
    /// Mirror of ScheduleEffects.GetMusicSectionModalItemCountAsync.
    /// When MusicLanguageCode is null, effective language is "E".
    /// </summary>
    private static async Task<int?> GetMusicSectionModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var publicationCode = schedule.MusicPublicationCode;
        if (string.IsNullOrWhiteSpace(publicationCode) || !PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return 0;
        }

        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();

        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        query = query.Where(sl =>
            (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
            sl.LanguageId == null);

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync();
    }
}
