#nullable enable

using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler;

public sealed record LoadMusicForSelectionArgs(
    int ScheduleId,
    bool IsNewSchedule,
    AlarmMusic? CurrentMusic,
    string? PublicationCode,
    string? LanguageCode,
    string? TrackCode,
    bool? Repeat);

public sealed record LoadBiblePublicationScheduleCodes(
    string? LanguageCode,
    string? PublicationCode,
    string? SectionCode,
    string? TrackCode);

public sealed record LoadBiblePublicationForSelectionArgs(
    int ScheduleId,
    bool IsNewSchedule,
    BiblePublicationSchedule? CurrentBiblePublication,
    LoadBiblePublicationScheduleCodes Codes,
    TimeSpan? FinishedDuration);
