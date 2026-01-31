#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
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
/// Cascade order: MusicType → Language → Publication → Section → Track
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
                return;
            }

            var musicType = currentSchedule.MusicType;
            var languageCode = currentSchedule.MusicLanguageCode;
            var publicationCode = currentSchedule.MusicPublicationCode;
            var sectionCode = currentSchedule.MusicSectionCode;
            var trackNumber = currentSchedule.MusicTrackNumber;

            // Cascade 1: MusicType selected but language not (for VocalMusic) → populate language, publication, section, track
            if (musicType == MusicType.VocalMusic && string.IsNullOrWhiteSpace(languageCode))
            {
                await HandleMusicTypeCascadeAsync(currentSchedule, dispatcher);
                return; // MusicType cascade handles everything below
            }

            // Cascade 1b: MusicType is Music (Instrumental) but publication not → populate publication, section, track
            if (musicType == MusicType.Music && string.IsNullOrWhiteSpace(publicationCode))
            {
                await HandleLanguageCascadeAsync(currentSchedule, dispatcher);
                return; // Language cascade handles everything below (including Instrumental Music)
            }

            // Cascade 2: Language selected but publication not → populate publication, section, track
            if (!string.IsNullOrWhiteSpace(languageCode) && string.IsNullOrWhiteSpace(publicationCode))
            {
                await HandleLanguageCascadeAsync(currentSchedule, dispatcher);
                return; // Language cascade handles everything below
            }

            // Cascade 3: Publication selected but section/track not → populate section, track
            // Only if section is not set (if section is set, go to cascade 4)
            if (!string.IsNullOrWhiteSpace(publicationCode) && 
                string.IsNullOrWhiteSpace(sectionCode))
            {
                await HandlePublicationCascadeAsync(currentSchedule, dispatcher);
                return; // Publication cascade handles section and track
            }

            // Cascade 4: Section selected but track not → populate track
            if (!string.IsNullOrWhiteSpace(sectionCode) &&
                (!trackNumber.HasValue || trackNumber.Value <= 0))
            {
                await HandleSectionCascadeAsync(currentSchedule, dispatcher);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MusicCascadeHandler: Error during cascade");
        }
    }

    private async Task HandleMusicTypeCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var musicType = currentSchedule.MusicType!.Value;

        logger.Information("MusicCascadeHandler: MusicType cascade - musicType={MusicType}",
            musicType);

        // For VocalMusic, we need a language - default to English
        if (musicType == MusicType.VocalMusic)
        {
            var languages = await mediaService.GetBiblePublicationLanguages("Music");
            if (languages.Count == 0)
            {
                logger.Warning("MusicCascadeHandler: No languages found for Music category");
                return;
            }

            // Default to English, fallback to first available
            var languageCode = languages.ContainsKey("E") ? "E" : languages.Keys.First();
            var language = languages[languageCode];

            // Now cascade to language
            var updatedSchedule = currentSchedule.DeepClone();
            updatedSchedule.MusicLanguageCode = languageCode;
            updatedSchedule.MusicLanguageName = language.Name;
            updatedSchedule.MusicLanguageDirection = language.Direction ?? "ltr";

            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
            
            // Language cascade will handle the rest
            return;
        }

        // For Music (instrumental), no language needed - get first publication without language
        await HandleLanguageCascadeAsync(currentSchedule, dispatcher);
    }

    private async Task HandleLanguageCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode ?? string.Empty;
        var musicType = currentSchedule.MusicType!.Value;

        logger.Information("MusicCascadeHandler: Language cascade - language={LanguageCode}, musicType={MusicType}",
            languageCode, musicType);

        // Get first publication for this language and music type
        string? publicationCode = null;
        string? publicationName = null;
        bool publicationWithoutLanguage = false;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        if (musicType == MusicType.VocalMusic && !string.IsNullOrEmpty(languageCode))
        {
            // For vocal music, get first publication with LanguageId
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
        }
        else if (musicType == MusicType.Music)
        {
            // For instrumental music, get first publication without LanguageId
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
            logger.Warning("MusicCascadeHandler: No publication found for language={LanguageCode}, musicType={MusicType}",
                languageCode, musicType);
            return;
        }

        // Get section and track
        var (sectionCode, sectionName, trackNum, trackTitle) =
            await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(
                mediaService,
                languageCode,
                publicationCode,
                publicationWithoutLanguage);

        if (trackNum <= 0)
        {
            logger.Warning("MusicCascadeHandler: No valid track found for publication={PublicationCode}",
                publicationCode);
            return;
        }

        UpdateSchedule(currentSchedule, publicationCode, publicationName, sectionCode, sectionName, trackNum, trackTitle, dispatcher);
    }

    private async Task HandlePublicationCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode!;
        var publicationCode = currentSchedule.MusicPublicationCode!;
        var musicType = currentSchedule.MusicType!.Value;

        logger.Information("MusicCascadeHandler: Publication cascade - publication={PublicationCode}, language={LanguageCode}, musicType={MusicType}",
            publicationCode, languageCode, musicType);

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

        var (sectionCode, sectionName, trackNum, trackTitle) =
            await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(
                mediaService,
                languageCode,
                publicationCode,
                publicationWithoutLanguage);

        if (trackNum <= 0)
        {
            logger.Warning("MusicCascadeHandler: No valid track found for publication={PublicationCode}",
                publicationCode);
            return;
        }

        UpdateSchedule(currentSchedule, publicationCode, publication.Name, sectionCode, sectionName, trackNum, trackTitle, dispatcher);
    }

    private async Task HandleSectionCascadeAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher)
    {
        var languageCode = currentSchedule.MusicLanguageCode!;
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

        SortedDictionary<int, BiblePublicationTrack>? tracks = null;
        int sectionIndex = 0;

        if (publication.LanguageId == null)
        {
            // Publication without language - use GetBiblePublicationTracks with empty language code
            sectionIndex = Bible.Alarm.Shared.Helpers.SectionCodeHelper.GetSectionIndexOrZero(sectionCode);
            tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionIndex);
        }
        else
        {
            // Publication with language
            var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
            if (sections != null)
            {
                var section = sections.Values.FirstOrDefault(s => s.SectionCode == sectionCode);
                if (section != null)
                {
                    sectionIndex = sections.FirstOrDefault(kvp => kvp.Value.SectionCode == sectionCode).Key;
                    tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionIndex);
                }
            }
        }

        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning("MusicCascadeHandler: No tracks found for sectionCode={SectionCode}", sectionCode);
            return;
        }

        var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
        var sectionName = currentSchedule.MusicSectionName ?? string.Empty;
        
        UpdateSchedule(currentSchedule, publicationCode, currentSchedule.MusicPublicationName, sectionCode, sectionName, firstTrack.Number, firstTrack.Title ?? string.Empty, dispatcher);
    }

    private void UpdateSchedule(ScheduleStateItem currentSchedule, string publicationCode, string? publicationName,
        string? sectionCode, string sectionName, int trackNumber, string trackTitle, IDispatcher dispatcher)
    {
        // Check if values have actually changed to prevent cascade cycles
        var currentSectionCode = currentSchedule.MusicSectionCode;
        var currentTrackNum = currentSchedule.MusicTrackNumber;
        var currentTrackTitle = currentSchedule.MusicTrackName;
        
        // If all values are already set correctly, don't dispatch to prevent infinite loop
        if (currentSchedule.MusicPublicationCode == publicationCode &&
            currentSectionCode == sectionCode &&
            currentTrackNum == trackNumber &&
            currentTrackTitle == trackTitle)
        {
            // Values haven't changed, skip dispatch to prevent cascade cycle
            logger.Debug("MusicCascadeHandler: Values unchanged, skipping dispatch to prevent cycle. publication={PublicationCode}, section={SectionCode}, track={TrackNumber}",
                publicationCode, sectionCode ?? "null", trackNumber);
            return;
        }

        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.MusicPublicationCode = publicationCode;
        if (!string.IsNullOrWhiteSpace(publicationName))
        {
            updatedSchedule.MusicPublicationName = publicationName;
        }
        updatedSchedule.MusicSectionCode = sectionCode;
        // Do not overwrite a populated display name with empty/whitespace (can happen on partial harvest / transient failures)
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            updatedSchedule.MusicSectionName = sectionName;
        }
        updatedSchedule.MusicTrackNumber = trackNumber;
        // Do not overwrite a populated display name with empty/whitespace (can happen on partial harvest / transient failures)
        if (!string.IsNullOrWhiteSpace(trackTitle))
        {
            updatedSchedule.MusicTrackName = trackTitle;
        }
        
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
    }
}
