#nullable enable

using Bible;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Services.Media.PlaylistInternal;

/// <summary>
/// Internal result type for next track resolution in PlaylistService.
/// </summary>
internal sealed record NextTrackInfo(
    string? NextTrackCode,
    KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>? NextTrack,
    string? NextSectionCode = null);
