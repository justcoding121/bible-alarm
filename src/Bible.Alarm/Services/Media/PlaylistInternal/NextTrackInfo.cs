#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Internal result type for next track resolution in PlaylistService.
/// </summary>
internal sealed record NextTrackInfo(
    string? NextTrackCode,
    KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>? NextTrack);
