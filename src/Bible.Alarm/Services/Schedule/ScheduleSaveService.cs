#nullable enable
using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleSaveService : IScheduleSaveService
{
    private readonly ILogger logger;
    private readonly IMapper mapper;

    public ScheduleSaveService(ILogger logger, IMapper mapper)
    {
        this.logger = logger;
        this.mapper = mapper;
    }

    public AlarmSchedule PrepareModelForSave(
        ScheduleStateItem currentSchedule,
        bool isNewSchedule,
        bool musicUpdated)
    {
        logger.Information("PrepareModelForSave: Starting. musicUpdated={MusicUpdated}, IsNewSchedule={IsNewSchedule}", musicUpdated, isNewSchedule);

        logger.Information("PrepareModelForSave: CurrentSchedule state - MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicId={MusicId}",
            currentSchedule?.MusicType?.ToString() ?? "null",
            currentSchedule?.MusicTrackNumber?.ToString() ?? "null",
            currentSchedule?.MusicPublicationCode ?? "null",
            currentSchedule?.MusicLanguageCode ?? "null",
            currentSchedule?.MusicId?.ToString() ?? "null");

        var model = mapper.Map<AlarmSchedule>(currentSchedule);

        logger.Information("PrepareModelForSave: After GetModel() - model.Music={HasMusic}, model.Music?.MusicType={MusicType}, model.Music?.TrackNumber={TrackNumber}, model.Music?.PublicationCode={PublicationCode}, model.Music?.LanguageCode={LanguageCode}",
            model.Music != null ? "not null" : "null",
            model.Music?.MusicType.ToString() ?? "null",
            model.Music?.TrackNumber.ToString() ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

        EnsureDefaultPublicationCode(model);

        // If music was updated, ensure model.Music has the correct music type from state
        if (musicUpdated && currentSchedule != null)
        {
            logger.Information("PrepareModelForSave: musicUpdated=true, updating model.Music from state");
            if (currentSchedule.MusicType.HasValue &&
                currentSchedule.MusicTrackNumber.HasValue &&
                currentSchedule.MusicTrackNumber.Value > 0)
            {
                if (model.Music == null)
                {
                    logger.Information("PrepareModelForSave: model.Music is null, creating new AlarmMusic from state");
                    model.Music = new AlarmMusic
                    {
                        Id = currentSchedule.MusicId ?? 0,
                        MusicType = currentSchedule.MusicType.Value,
                        PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                        LanguageCode = currentSchedule.MusicLanguageCode,
                        TrackNumber = currentSchedule.MusicTrackNumber.Value,
                        Repeat = currentSchedule.MusicRepeat ?? false,
                        AlarmScheduleId = model.Id
                    };
                    logger.Information("PrepareModelForSave: Created model.Music from state. MusicType={MusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        model.Music.MusicType, model.Music.TrackNumber, model.Music.PublicationCode, model.Music.LanguageCode);
                }
                else
                {
                    var oldMusicType = model.Music.MusicType;
                    var oldTrackNumber = model.Music.TrackNumber;
                    model.Music.MusicType = currentSchedule.MusicType.Value;
                    model.Music.PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty;
                    model.Music.LanguageCode = currentSchedule.MusicLanguageCode;
                    model.Music.TrackNumber = currentSchedule.MusicTrackNumber.Value;
                    model.Music.Repeat = currentSchedule.MusicRepeat ?? false;
                    if (currentSchedule.MusicId.HasValue)
                    {
                        model.Music.Id = currentSchedule.MusicId.Value;
                    }
                    logger.Information("PrepareModelForSave: Updated model.Music from state. Old MusicType={OldMusicType} -> New MusicType={NewMusicType}, Old TrackNumber={OldTrackNumber} -> New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        oldMusicType, model.Music.MusicType, oldTrackNumber, model.Music.TrackNumber, model.Music.PublicationCode, model.Music.LanguageCode);
                }
            }
            else
            {
                logger.Warning("PrepareModelForSave: musicUpdated=true but CurrentSchedule music properties are invalid. MusicType={MusicType}, MusicTrackNumber={TrackNumber}",
                    currentSchedule?.MusicType?.ToString() ?? "null",
                    currentSchedule?.MusicTrackNumber?.ToString() ?? "null");
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
            model.MusicEnabled = currentSchedule.MusicEnabled;
        }
        logger.Information("PrepareModelForSave: Set model.MusicEnabled={MusicEnabled} from CurrentSchedule state",
            model.MusicEnabled);

        logger.Information("PrepareModelForSave: Final model - Model.Id={ModelId}, Model.Name={ModelName}, HasMusic={HasMusic}, MusicEnabled={MusicEnabled}, MusicType={MusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            model.Id, model.Name, model.Music != null, model.MusicEnabled,
            model.Music?.MusicType.ToString() ?? "null",
            model.Music?.TrackNumber.ToString() ?? "null",
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

        // Ensure MusicEnabled and all display names are set from CurrentSchedule state
        scheduleStateItem.MusicEnabled = currentSchedule.MusicEnabled;
        logger.Information("PrepareScheduleStateItem: Set scheduleStateItem.MusicEnabled={MusicEnabled} from CurrentSchedule state",
            scheduleStateItem.MusicEnabled);

        // Preserve all display names from CurrentSchedule state
        scheduleStateItem.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
        scheduleStateItem.BiblePublicationName = currentSchedule.BiblePublicationName;
        scheduleStateItem.BiblePublicationSectionName = currentSchedule.BiblePublicationSectionName;
        scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
        scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
        scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

        logger.Information("PrepareScheduleStateItem: Preserved display names from CurrentSchedule state. BiblePublicationLanguageName={LanguageName}, BiblePublicationName={PublicationName}, MusicTrackName={MusicTrackName}",
            scheduleStateItem.BiblePublicationLanguageName ?? "null",
            scheduleStateItem.BiblePublicationName ?? "null",
            scheduleStateItem.MusicTrackName ?? "null");

        // If music was updated, always use music properties from CurrentSchedule state
        if (musicUpdated)
        {
            logger.Information("PrepareScheduleStateItem: musicUpdated=true, overriding with CurrentSchedule state");
            if (currentSchedule.MusicType.HasValue &&
                currentSchedule.MusicTrackNumber.HasValue &&
                currentSchedule.MusicTrackNumber.Value > 0)
            {
                var oldMusicType = scheduleStateItem.MusicType;
                scheduleStateItem.MusicType = currentSchedule.MusicType;
                scheduleStateItem.MusicTrackNumber = currentSchedule.MusicTrackNumber;
                scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
                scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
                scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
                scheduleStateItem.MusicId = currentSchedule.MusicId;
                scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
                scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
                scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

                logger.Information("PrepareScheduleStateItem: Overrode music properties from CurrentSchedule state. Old MusicType={OldMusicType} -> New MusicType={NewMusicType}, TrackNumber={TrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}, Repeat={Repeat}",
                    oldMusicType?.ToString() ?? "null", scheduleStateItem.MusicType?.ToString() ?? "null",
                    scheduleStateItem.MusicTrackNumber, scheduleStateItem.MusicPublicationCode, scheduleStateItem.MusicLanguageCode, scheduleStateItem.MusicRepeat);
            }
            else
            {
                logger.Warning("PrepareScheduleStateItem: musicUpdated=true but CurrentSchedule music properties are invalid. MusicType={MusicType}, MusicTrackNumber={TrackNumber}",
                    currentSchedule?.MusicType?.ToString() ?? "null",
                    currentSchedule?.MusicTrackNumber?.ToString() ?? "null");
            }
        }
        // Populate default music properties when music is disabled
        else if (!musicUpdated && (!model.MusicEnabled || model.Music == null))
        {
            var needsDefaultMusic = !scheduleStateItem.MusicType.HasValue ||
                                   !scheduleStateItem.MusicTrackNumber.HasValue ||
                                   scheduleStateItem.MusicTrackNumber.Value <= 0;

            if (needsDefaultMusic &&
                currentSchedule.MusicType.HasValue &&
                currentSchedule.MusicTrackNumber.HasValue &&
                currentSchedule.MusicTrackNumber.Value > 0)
            {
                scheduleStateItem.MusicType = currentSchedule.MusicType;
                scheduleStateItem.MusicTrackNumber = currentSchedule.MusicTrackNumber;
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
            scheduleStateItem.MusicType = currentSchedule.MusicType;
            scheduleStateItem.MusicTrackNumber = currentSchedule.MusicTrackNumber;
            scheduleStateItem.MusicPublicationCode = currentSchedule.MusicPublicationCode;
            scheduleStateItem.MusicLanguageCode = currentSchedule.MusicLanguageCode;
            scheduleStateItem.MusicRepeat = currentSchedule.MusicRepeat;
            scheduleStateItem.MusicId = currentSchedule.MusicId;
            scheduleStateItem.MusicLanguageName = currentSchedule.MusicLanguageName;
            scheduleStateItem.MusicPublicationName = currentSchedule.MusicPublicationName;
            scheduleStateItem.MusicTrackName = currentSchedule.MusicTrackName;

            logger.Debug("PrepareScheduleStateItem: Preserved music properties from current schedule state. MusicType={MusicType}, TrackNumber={TrackNumber}",
                scheduleStateItem.MusicType, scheduleStateItem.MusicTrackNumber);
        }

        logger.Information("PrepareScheduleStateItem: Final scheduleStateItem before dispatch - MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicId={MusicId}",
            scheduleStateItem.MusicType?.ToString() ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicId?.ToString() ?? "null");

        return scheduleStateItem;
    }
}

