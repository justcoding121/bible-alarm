#nullable enable

using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Mapping;

/// <summary>
/// Nullable nested-entity projections used by <see cref="ScheduleMappingProfile"/> when mapping AlarmSchedule ↔ ScheduleStateItem.
/// </summary>
internal static class AlarmScheduleToScheduleStateItemMap
{
    public static int? BiblePublicationScheduleId(AlarmSchedule src) =>
        src.BiblePublicationSchedule != null ? (int?)src.BiblePublicationSchedule.Id : null;

    public static string? BiblePublicationLanguageCode(AlarmSchedule src) =>
        src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.LanguageCode : null;

    public static string? BiblePublicationCode(AlarmSchedule src) =>
        src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.PublicationCode : null;

    public static string? BiblePublicationSectionCode(AlarmSchedule src) =>
        src.BiblePublicationSchedule != null ? SectionCodeHelper.Normalize(src.BiblePublicationSchedule.SectionCode) : null;

    public static string? BiblePublicationTrackCode(AlarmSchedule src) =>
        src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.TrackCode : null;

    public static TimeSpan? BiblePublicationFinishedDuration(AlarmSchedule src) =>
        src.BiblePublicationSchedule != null ? (TimeSpan?)src.BiblePublicationSchedule.FinishedDuration : null;

    public static int? MusicId(AlarmSchedule src) =>
        src.Music != null ? (int?)src.Music.Id : null;

    public static string? MusicPublicationCode(AlarmSchedule src) =>
        src.Music != null ? src.Music.PublicationCode : null;

    public static string? MusicLanguageCode(AlarmSchedule src) =>
        src.Music != null ? src.Music.LanguageCode : null;

    public static string? MusicSectionCode(AlarmSchedule src) =>
        src.Music != null ? src.Music.SectionCode : null;

    public static string? MusicTrackCode(AlarmSchedule src) =>
        src.Music != null ? src.Music.TrackCode : null;

    public static bool? MusicRepeat(AlarmSchedule src) =>
        src.Music != null ? (bool?)src.Music.Repeat : null;
}

internal static class ScheduleStateItemToAlarmScheduleMap
{
    public static BiblePublicationSchedule? BiblePublicationSchedule(ScheduleStateItem src) =>
        src.BiblePublicationScheduleId.HasValue
            ? new BiblePublicationSchedule
            {
                Id = src.BiblePublicationScheduleId.Value,
                LanguageCode = src.BiblePublicationLanguageCode,
                PublicationCode = src.BiblePublicationCode ?? string.Empty,
                SectionCode = SectionCodeHelper.Normalize(src.BiblePublicationSectionCode),
                TrackCode = src.BiblePublicationTrackCode ?? string.Empty,
                FinishedDuration = src.BiblePublicationFinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = src.Id
            }
            : null;

    public static AlarmMusic? Music(ScheduleStateItem src) =>
        src.MusicId.HasValue || !string.IsNullOrEmpty(src.MusicPublicationCode)
            ? new AlarmMusic
            {
                Id = src.MusicId ?? 0,
                PublicationCode = src.MusicPublicationCode ?? string.Empty,
                LanguageCode = src.MusicLanguageCode,
                SectionCode = src.MusicSectionCode,
                TrackCode = src.MusicTrackCode ?? string.Empty,
                Repeat = src.MusicRepeat ?? false,
                AlarmScheduleId = src.Id
            }
            : null;
}
