#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Groups injected collaborators for <see cref="global::Bible.Alarm.Services.Media.PlaybackService"/> to satisfy constructor parameter limits.
/// </summary>
public sealed record PlaybackServiceInjectionContext(
    IPreparePlaybackService PreparePlaybackService,
    IPlaylistService PlaylistService,
    IFallbackAlarmSoundService FallbackAlarmSoundService,
    IMediaCacheService MediaCacheService,
    ICdnPlaybackUrlProbe CdnPlaybackUrlProbe,
    ITrackCdnUrlRefresher TrackCdnUrlRefresher,
    INotificationService NotificationService,
    IDefaultDeviceRingtoneService DefaultDeviceRingtoneService);
