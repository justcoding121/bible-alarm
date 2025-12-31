#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleInitializationService
{
    Task<ScheduleStateItem> InitializeNewScheduleAsync();

    Task CompleteScheduleLoadAsync();

    void InitializeTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref int lastScheduleId,
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat);
}

