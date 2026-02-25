#nullable enable
using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleSaveService : IScheduleSaveService
{
    private readonly ILogger logger;
    private readonly IMapper mapper;
    private readonly IMediaService? mediaService;

    public ScheduleSaveService(ILogger logger, IMapper mapper, IMediaService? mediaService = null)
    {
        this.logger = logger;
        this.mapper = mapper;
        this.mediaService = mediaService;
    }

    public async Task<AlarmSchedule> PrepareModelForSaveAsync(
        ScheduleStateItem currentSchedule,
        bool isNewSchedule,
        bool musicUpdated)
    {
        logger.Information("PrepareModelForSave: Starting. musicUpdated={MusicUpdated}, IsNewSchedule={IsNewSchedule}", musicUpdated, isNewSchedule);

        logger.Information("PrepareModelForSave: CurrentSchedule state - MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicTrackCode={TrackCode}, MusicId={MusicId}, NumberOfTracksToPlay={NumberOfTracksToPlay}, AlwaysPlayFromStart={AlwaysPlayFromStart}",
            currentSchedule?.MusicPublicationCode ?? "null",
            currentSchedule?.MusicLanguageCode ?? "null",
            currentSchedule?.MusicTrackCode?.ToString() ?? "null",
            currentSchedule?.MusicId?.ToString() ?? "null",
            currentSchedule?.NumberOfTracksToPlay ?? 0,
            currentSchedule?.AlwaysPlayFromStart ?? false);

        var model = mapper.Map<AlarmSchedule>(currentSchedule);

        // Explicitly ensure NumberOfTracksToPlay and AlwaysPlayFromStart are set from currentSchedule state
        // (AutoMapper should handle this, but we explicitly set it to be safe)
        model.NumberOfTracksToPlay = currentSchedule?.NumberOfTracksToPlay ?? 0;
        model.AlwaysPlayFromStart = currentSchedule?.AlwaysPlayFromStart ?? false;

        var pubCode = currentSchedule?.BiblePublicationCode;
        var isMusicPublication = (currentSchedule?.BiblePublicationIsMusic ?? false) ||
            (!string.IsNullOrWhiteSpace(pubCode) && JwSourceHelper.MusicFlagPublicationCodes.Contains(pubCode));
        model.CategoryCode = isMusicPublication ? null : (string.IsNullOrWhiteSpace(currentSchedule?.BiblePublicationCategoryName) ? null : currentSchedule.BiblePublicationCategoryName);

        // For Bible/Music schedule content, always keep the selected language (even for no-language pubs) so the schedule page can show language + pubs. Only normalize begin-with-music (AlarmMusic) for no-language.
        await NormalizeLanguageCodeForNoLanguagePublicationsAsync(model);
        
        logger.Information("PrepareModelForSave: After mapping - model.NumberOfTracksToPlay={NumberOfTracksToPlay}, model.AlwaysPlayFromStart={AlwaysPlayFromStart}",
            model.NumberOfTracksToPlay, model.AlwaysPlayFromStart);

        logger.Information("PrepareModelForSave: After GetModel() - model.Music={HasMusic}, model.Music?.TrackCode={TrackCode}, model.Music?.PublicationCode={PublicationCode}, model.Music?.LanguageCode={LanguageCode}",
            model.Music != null ? "not null" : "null",
            model.Music?.TrackCode ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

        EnsureDefaultPublicationCode(model);

        // If music was updated, ensure model.Music has the correct properties from state
        // Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal
        if (musicUpdated && currentSchedule != null)
        {
            logger.Information("PrepareModelForSave: musicUpdated=true, updating model.Music from state");
            if (!string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
                !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
            {
                if (model.Music == null)
                {
                    logger.Information("PrepareModelForSave: model.Music is null, creating new AlarmMusic from state");
                    model.Music = new AlarmMusic
                    {
                        Id = currentSchedule.MusicId ?? 0,
                        PublicationCode = currentSchedule.MusicPublicationCode,
                        LanguageCode = currentSchedule.MusicLanguageCode,
                        SectionCode = currentSchedule.MusicSectionCode,
                        TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
                        Repeat = currentSchedule.MusicRepeat ?? false,
                        AlarmScheduleId = model.Id
                    };
                    logger.Information("PrepareModelForSave: Created model.Music from state. TrackCode={TrackCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        model.Music.TrackCode, model.Music.PublicationCode, model.Music.LanguageCode);
                }
                else
                {
                    var oldTrackCode = model.Music.TrackCode;
                    model.Music.PublicationCode = currentSchedule.MusicPublicationCode;
                    model.Music.LanguageCode = currentSchedule.MusicLanguageCode;
                    model.Music.SectionCode = currentSchedule.MusicSectionCode;
                    model.Music.TrackCode = currentSchedule.MusicTrackCode ?? string.Empty;
                    model.Music.Repeat = currentSchedule.MusicRepeat ?? false;
                    if (currentSchedule.MusicId.HasValue)
                    {
                        model.Music.Id = currentSchedule.MusicId.Value;
                    }
                    logger.Information("PrepareModelForSave: Updated model.Music from state. Old TrackCode={OldTrackCode} -> New TrackCode={NewTrackCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        oldTrackCode, model.Music.TrackCode, model.Music.PublicationCode, model.Music.LanguageCode);
                }
            }
            else
            {
                logger.Warning("PrepareModelForSave: musicUpdated=true but CurrentSchedule music properties are invalid. MusicPublicationCode={PublicationCode}, MusicTrackCode={TrackCode}",
                    currentSchedule?.MusicPublicationCode ?? "null",
                    currentSchedule?.MusicTrackCode?.ToString() ?? "null");
            }
        }
        else
        {
            logger.Information("PrepareModelForSave: musicUpdated=false, skipping music update");
        }

        ClearUnchangedMusic(model, isNewSchedule, musicUpdated);

        // Ensure MusicEnabled is set from CurrentSchedule state
        if (currentSchedule != null)
        {
            if (isMusicPublication)
            {
                model.MusicEnabled = false;
                model.Music = null;
                logger.Information("PrepareModelForSave: Music publication selected - disabled music and cleared music data");
            }
            else
            {
                model.MusicEnabled = currentSchedule.MusicEnabled;
            }
        }
        logger.Information("PrepareModelForSave: Set model.MusicEnabled={MusicEnabled} from CurrentSchedule state",
            model.MusicEnabled);

        logger.Information("PrepareModelForSave: Final model - Model.Id={ModelId}, Model.Name={ModelName}, HasMusic={HasMusic}, MusicEnabled={MusicEnabled}, TrackCode={TrackCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            model.Id, model.Name, model.Music != null, model.MusicEnabled,
            model.Music?.TrackCode ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

        return model;
    }


    private void EnsureDefaultPublicationCode(AlarmSchedule model)
    {
        if (model.BiblePublicationSchedule != null && string.IsNullOrWhiteSpace(model.BiblePublicationSchedule.PublicationCode))
        {
            logger.Warning("SaveAsync: BiblePublicationSchedule has empty PublicationCode, defaulting to 'nwt' (2013)");
            model.BiblePublicationSchedule.PublicationCode = "nwt";
        }
    }

    private void ClearUnchangedMusic(AlarmSchedule model, bool isNewSchedule, bool musicUpdated)
    {
        if (!isNewSchedule && !musicUpdated)
        {
            model.Music = null;
            logger.Debug("SaveAsync: Music set to null for existing schedule (not updated)");
        }
    }

    public ScheduleStateItem PrepareScheduleStateItem(
        AlarmSchedule model,
        ScheduleStateItem currentSchedule,
        bool musicUpdated)
    {
        logger.Information("PrepareScheduleStateItem: Starting. IsNewSchedule={IsNewSchedule}, MusicUpdated={MusicUpdated}",
            model.Id <= 0, musicUpdated);

        var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);

        scheduleStateItem.BiblePublicationCategoryId = currentSchedule.BiblePublicationCategoryId;
        scheduleStateItem.BiblePublicationCategoryName = currentSchedule.BiblePublicationCategoryName;

        var isMusicPub = currentSchedule.BiblePublicationIsMusic ||
            (!string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationCode) && JwSourceHelper.MusicFlagPublicationCodes.Contains(currentSchedule.BiblePublicationCode));
        scheduleStateItem.BiblePublicationIsMusic = isMusicPub;
        if (isMusicPub)
        {
            scheduleStateItem.MusicEnabled = false;
            scheduleStateItem.MusicTrackCode = null;
            scheduleStateItem.MusicPublicationCode = null;
            scheduleStateItem.MusicLanguageCode = null;
            scheduleStateItem.MusicSectionCode = null;
            scheduleStateItem.MusicRepeat = null;
            scheduleStateItem.MusicId = null;
            logger.Information("PrepareScheduleStateItem: Music publication selected - disabled music and cleared music data");
        }
        else
        {
            scheduleStateItem.MusicEnabled = currentSchedule.MusicEnabled;
        }
        scheduleStateItem.NumberOfTracksToPlay = currentSchedule.NumberOfTracksToPlay;
        scheduleStateItem.AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
        logger.Information("PrepareScheduleStateItem: Set scheduleStateItem.MusicEnabled={MusicEnabled}, NumberOfTracksToPlay={NumberOfTracksToPlay}, and AlwaysPlayFromStart={AlwaysPlayFromStart} from CurrentSchedule state",
            scheduleStateItem.MusicEnabled, scheduleStateItem.NumberOfTracksToPlay, scheduleStateItem.AlwaysPlayFromStart);

        // Preserve all display names from CurrentSchedule state
        scheduleStateItem.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
        scheduleStateItem.BiblePublicationName = currentSchedule.BiblePublicationName;
        
        // For non-sectioned publications, clear the section name and use track title (e.g., dramas, videos)
        if (!PublicationTypeHelper.HasSectionStructure(currentSchedule.BiblePublicationCode))
        {
            scheduleStateItem.BiblePublicationSectionName = null;
            // For dramas/videos, the track title is used as subtitle instead of section name
            scheduleStateItem.BiblePublicationTrackTitle = currentSchedule.BiblePublicationTrackTitle;
        }
        else
        {
            scheduleStateItem.BiblePublicationSectionName = currentSchedule.BiblePublicationSectionName;
            scheduleStateItem.BiblePublicationTrackTitle = currentSchedule.BiblePublicationTrackTitle;
        }
        
        scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
        scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
        scheduleStateItem.MusicSectionName = currentSchedule.MusicSectionName;
        scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

        logger.Information("PrepareScheduleStateItem: Preserved display names from CurrentSchedule state. BiblePublicationLanguageName={LanguageName}, BiblePublicationName={PublicationName}, MusicTrackName={MusicTrackName}",
            scheduleStateItem.BiblePublicationLanguageName ?? "null",
            scheduleStateItem.BiblePublicationName ?? "null",
            scheduleStateItem.MusicTrackName ?? "null");

        if (musicUpdated && !isMusicPub)
        {
            logger.Information("PrepareScheduleStateItem: musicUpdated=true, overriding with CurrentSchedule state");
            if (!string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
                !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
            {
                scheduleStateItem.MusicTrackCode = currentSchedule.MusicTrackCode;
                scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
                scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
                scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
                scheduleStateItem.MusicId = currentSchedule.MusicId;
                scheduleStateItem.MusicSectionCode = currentSchedule.MusicSectionCode;
                scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
                scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
                scheduleStateItem.MusicSectionName = currentSchedule.MusicSectionName;
                scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

                logger.Information("PrepareScheduleStateItem: Overrode music properties from CurrentSchedule state. TrackCode={TrackCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}, Repeat={Repeat}",
                    scheduleStateItem.MusicTrackCode, scheduleStateItem.MusicPublicationCode, scheduleStateItem.MusicLanguageCode, scheduleStateItem.MusicRepeat);
            }
            else
            {
                logger.Warning("PrepareScheduleStateItem: musicUpdated=true but CurrentSchedule music properties are invalid. MusicPublicationCode={PublicationCode}, MusicTrackCode={TrackCode}",
                    currentSchedule?.MusicPublicationCode ?? "null",
                    currentSchedule?.MusicTrackCode?.ToString() ?? "null");
            }
        }
        // Populate default music properties when music is disabled
        // Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal
        else if (!musicUpdated && (!model.MusicEnabled || model.Music == null))
        {
            var needsDefaultMusic = string.IsNullOrEmpty(scheduleStateItem.MusicPublicationCode) ||
                                   string.IsNullOrWhiteSpace(scheduleStateItem.MusicTrackCode);

            if (needsDefaultMusic &&
                !string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
                !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
            {
                scheduleStateItem.MusicTrackCode = currentSchedule.MusicTrackCode;
                scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
                scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
                scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
                scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

                logger.Debug("PrepareScheduleStateItem: Using music properties from CurrentSchedule state (no DB query)");
            }
        }
        // For existing schedules where music is enabled but not updated, preserve existing music properties
        else if (model.Id > 0 && !musicUpdated && model.Music == null)
        {
            scheduleStateItem.MusicTrackCode = currentSchedule.MusicTrackCode;
            scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
            scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
            scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
            scheduleStateItem.MusicId = currentSchedule.MusicId;
            scheduleStateItem.MusicSectionCode = currentSchedule.MusicSectionCode;
            scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
            scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
            scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

            logger.Debug("PrepareScheduleStateItem: Preserved music properties from current schedule state. PublicationCode={PublicationCode}, TrackCode={TrackCode}",
                scheduleStateItem.MusicPublicationCode, scheduleStateItem.MusicTrackCode);
        }

        logger.Information("PrepareScheduleStateItem: Final scheduleStateItem before dispatch - MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicTrackCode={TrackCode}, MusicId={MusicId}",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicTrackCode?.ToString() ?? "null",
            scheduleStateItem.MusicId?.ToString() ?? "null");

        return scheduleStateItem;
    }

    /// <summary>
    /// No longer normalizes Music.LanguageCode for no-language publications.
    /// For no-language music (e.g. iam), we store the schedule's current language (e.g. MY) in Music.LanguageCode,
    /// same as Bible container stores BiblePublicationSchedule.LanguageCode, so the schedule page shows the correct language when viewed again.
    /// </summary>
    private Task NormalizeLanguageCodeForNoLanguagePublicationsAsync(AlarmSchedule model)
    {
        return Task.CompletedTask;
    }
}

