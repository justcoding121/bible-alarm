#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;

namespace Bible.Alarm.Services.Media.PlaylistInternal;

/// <summary>
/// Internal result type for next track resolution in PlaylistService.
/// </summary>
internal sealed record NextTrackInfo(
    string? NextTrackCode,
    TrackNavigationResult? NextTrack,
    string? NextSectionCode = null);
