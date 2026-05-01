#nullable enable

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;

/// <summary>Piece of music cascade identity and modal badge counts dispatched into schedule state.</summary>
public sealed record MusicCascadeScheduleMutation(
    string PublicationCode,
    string? PublicationName,
    string? SectionCode,
    string SectionName,
    string TrackCode,
    string TrackTitle,
    int? PublicationModalItemCount,
    int? SectionModalItemCount);
