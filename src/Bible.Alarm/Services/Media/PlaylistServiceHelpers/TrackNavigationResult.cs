#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Result of track navigation that includes the resolved publication code.
/// Needed because cross-publication advance can change the publication.
/// </summary>
public sealed record TrackNavigationResult(
    string PublicationCode,
    BiblePublicationSection? Section,
    BiblePublicationTrack Track);
