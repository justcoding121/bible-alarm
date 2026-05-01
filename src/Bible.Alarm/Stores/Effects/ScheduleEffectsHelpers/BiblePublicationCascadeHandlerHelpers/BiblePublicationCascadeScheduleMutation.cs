#nullable enable

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;

/// <summary>Bible cascade identity, modal counts, and language-reset flag for schedule state dispatch.</summary>
public sealed record BiblePublicationCascadeScheduleMutation(
    string PublicationCode,
    string? PublicationName,
    string? SectionCode,
    string SectionName,
    string TrackCode,
    string TrackTitle,
    int? PublicationModalItemCount,
    int? SectionModalItemCount,
    int? TrackModalItemCount,
    bool PublicationWithoutLanguage = false);
