#nullable enable
using AutoMapper;
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

    public ScheduleSaveService(ILogger logger, IMapper mapper)
    {
        this.logger = logger;
        this.mapper = mapper;
    }

    public async Task<AlarmSchedule> PrepareModelForSaveAsync(
        ScheduleStateItem currentSchedule,
        bool isNewSchedule,
        bool musicUpdated)
    {
        logger.Information(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveStarting, musicUpdated, isNewSchedule);

        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveCurrentScheduleState,
            currentSchedule?.MusicPublicationCode ?? "null",
            currentSchedule?.MusicLanguageCode ?? "null",
            currentSchedule?.MusicTrackCode?.ToString() ?? "null",
            currentSchedule?.MusicId?.ToString() ?? "null",
            currentSchedule?.NumberOfTracksToPlay ?? 0,
            currentSchedule?.AlwaysPlayFromStart ?? false);

        var model = mapper.Map<AlarmSchedule>(currentSchedule);

        model.NumberOfTracksToPlay = currentSchedule?.NumberOfTracksToPlay ?? 0;
        model.AlwaysPlayFromStart = currentSchedule?.AlwaysPlayFromStart ?? false;

        ApplyCategoryCodeFromSchedule(model, currentSchedule);

        await NormalizeLanguageCodeForNoLanguagePublicationsAsync();

        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveAfterMapping,
            model.NumberOfTracksToPlay, model.AlwaysPlayFromStart);

        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveAfterGetModel,
            model.Music != null ? "not null" : "null",
            model.Music?.TrackCode ?? "null",
            model.Music?.PublicationCode ?? "null",
            model.Music?.LanguageCode ?? "null");

        EnsureDefaultPublicationCode(model);

        var isMusicPublication = IsScheduleBiblePublicationMusic(currentSchedule);

        SyncMusicEntityWhenUpdated(model, currentSchedule, musicUpdated);

        ClearUnchangedMusic(model, isNewSchedule, musicUpdated);

        ApplyMusicEnabledBasedOnPublicationType(model, currentSchedule, isMusicPublication);
        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveSetMusicEnabledFromState,
            model.MusicEnabled);

        logger.Information(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveFinalModel,
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
            logger.Warning(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.SaveAsyncBiblePublicationEmptyPublicationCodeDefaultingNwt);
            model.BiblePublicationSchedule.PublicationCode = AppConstants.Media.BiblePublicationCodeNwt;
        }
    }

    private void ClearUnchangedMusic(AlarmSchedule model, bool isNewSchedule, bool musicUpdated)
    {
        if (!isNewSchedule && !musicUpdated)
        {
            model.Music = null;
            logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.SaveAsyncMusicNullExistingScheduleNotUpdated);
        }
    }

    public ScheduleStateItem PrepareScheduleStateItem(
        AlarmSchedule model,
        ScheduleStateItem currentSchedule,
        bool musicUpdated)
    {
        logger.Information(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemStarting,
            model.Id <= 0, musicUpdated);

        var scheduleStateItem = mapper.Map<ScheduleStateItem>(model);

        scheduleStateItem.BiblePublicationCategoryId = currentSchedule.BiblePublicationCategoryId;
        scheduleStateItem.BiblePublicationCategoryName = currentSchedule.BiblePublicationCategoryName;

        var isMusicPub = IsScheduleBiblePublicationMusic(currentSchedule);

        ApplyMusicFieldsWhenBibleIsMusicPublication(scheduleStateItem, currentSchedule, isMusicPub);

        scheduleStateItem.NumberOfTracksToPlay = currentSchedule.NumberOfTracksToPlay;
        scheduleStateItem.AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemSetMusicEnabledTracksAlwaysPlayFromStart,
            scheduleStateItem.MusicEnabled, scheduleStateItem.NumberOfTracksToPlay, scheduleStateItem.AlwaysPlayFromStart);

        CopyBibleAndMusicDisplayFields(scheduleStateItem, currentSchedule);

        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemPreservedDisplayNames,
            scheduleStateItem.BiblePublicationLanguageName ?? "null",
            scheduleStateItem.BiblePublicationName ?? "null",
            scheduleStateItem.MusicTrackName ?? "null");

        ApplyMusicOverridesAfterPrepare(scheduleStateItem, model, currentSchedule, musicUpdated, isMusicPub);

        logger.Information(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemFinalBeforeDispatch,
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicTrackCode?.ToString() ?? "null",
            scheduleStateItem.MusicId?.ToString() ?? "null");

        return scheduleStateItem;
    }

    private static bool IsScheduleBiblePublicationMusic(ScheduleStateItem? currentSchedule)
    {
        var pubCode = currentSchedule?.BiblePublicationCode;
        return (currentSchedule?.BiblePublicationIsMusic ?? false) ||
               (!string.IsNullOrWhiteSpace(pubCode) && JwSourceHelper.MusicFlagPublicationCodes.Contains(pubCode));
    }

    private static void ApplyCategoryCodeFromSchedule(AlarmSchedule model, ScheduleStateItem? currentSchedule)
    {
        if (IsScheduleBiblePublicationMusic(currentSchedule))
        {
            model.CategoryCode = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(currentSchedule?.BiblePublicationCategoryName))
        {
            model.CategoryCode = null;
        }
        else
        {
            model.CategoryCode = currentSchedule!.BiblePublicationCategoryName;
        }
    }

    private void SyncMusicEntityWhenUpdated(AlarmSchedule model, ScheduleStateItem? currentSchedule, bool musicUpdated)
    {
        if (!musicUpdated || currentSchedule == null)
        {
            logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveMusicUpdatedFalseSkippingMusicUpdate);
            return;
        }

        logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveMusicUpdatedUpdatingFromState);
        if (!string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
            !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode))
        {
            if (model.Music == null)
            {
                logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveModelMusicNullCreatingNew);
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
                logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveCreatedModelMusicFromState,
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

                logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveUpdatedModelMusicFromState,
                    oldTrackCode, model.Music.TrackCode, model.Music.PublicationCode, model.Music.LanguageCode);
            }
        }
        else
        {
            logger.Warning(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveMusicUpdatedInvalidMusicProperties,
                currentSchedule.MusicPublicationCode ?? "null",
                currentSchedule.MusicTrackCode?.ToString() ?? "null");
        }
    }

    private void ApplyMusicEnabledBasedOnPublicationType(
        AlarmSchedule model,
        ScheduleStateItem? currentSchedule,
        bool isMusicPublication)
    {
        if (currentSchedule == null)
        {
            return;
        }

        if (isMusicPublication)
        {
            model.MusicEnabled = false;
            model.Music = null;
            logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveMusicPublicationDisabledMusic);
        }
        else
        {
            model.MusicEnabled = currentSchedule.MusicEnabled;
        }
    }

    private void ApplyMusicFieldsWhenBibleIsMusicPublication(
        ScheduleStateItem scheduleStateItem,
        ScheduleStateItem currentSchedule,
        bool isMusicPub)
    {
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
            logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemMusicPublicationDisabledMusic);
        }
        else
        {
            scheduleStateItem.MusicEnabled = currentSchedule.MusicEnabled;
        }
    }

    private static void CopyBibleAndMusicDisplayFields(ScheduleStateItem scheduleStateItem, ScheduleStateItem currentSchedule)
    {
        scheduleStateItem.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
        scheduleStateItem.BiblePublicationName = currentSchedule.BiblePublicationName;

        if (!PublicationTypeHelper.HasSectionStructure(currentSchedule.BiblePublicationCode))
        {
            scheduleStateItem.BiblePublicationSectionName = null;
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
    }

    private void ApplyMusicOverridesAfterPrepare(
        ScheduleStateItem scheduleStateItem,
        AlarmSchedule model,
        ScheduleStateItem currentSchedule,
        bool musicUpdated,
        bool isMusicPub)
    {
        if (musicUpdated && !isMusicPub)
        {
            logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemMusicUpdatedOverridingWithCurrentSchedule);
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

                logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemOverrodeMusicPropertiesFromCurrentSchedule,
                    scheduleStateItem.MusicTrackCode, scheduleStateItem.MusicPublicationCode, scheduleStateItem.MusicLanguageCode, scheduleStateItem.MusicRepeat);
            }
            else
            {
                logger.Warning(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemMusicUpdatedInvalidMusicProperties,
                    currentSchedule.MusicPublicationCode ?? "null",
                    currentSchedule.MusicTrackCode?.ToString() ?? "null");
            }

            return;
        }

        if (!musicUpdated && (!model.MusicEnabled || model.Music == null))
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

                logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemUsingMusicPropertiesNoDbQuery);
            }

            return;
        }

        if (model.Id > 0 && !musicUpdated && model.Music == null)
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

            logger.Debug(AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemPreservedMusicPropertiesFromCurrentScheduleState,
                scheduleStateItem.MusicPublicationCode, scheduleStateItem.MusicTrackCode);
        }
    }

    /// <summary>
    /// No longer normalizes Music.LanguageCode for no-language publications.
    /// For no-language music (e.g. iam), we store the schedule's current language (e.g. MY) in Music.LanguageCode,
    /// same as Bible container stores BiblePublicationSchedule.LanguageCode, so the schedule page shows the correct language when viewed again.
    /// </summary>
    private static Task NormalizeLanguageCodeForNoLanguagePublicationsAsync()
    {
        return Task.CompletedTask;
    }
}

