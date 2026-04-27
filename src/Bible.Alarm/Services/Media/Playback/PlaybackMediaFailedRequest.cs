#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackEventHandler.HandleMediaFailedAsync"/>.
/// </summary>
public readonly record struct PlaybackMediaFailedRequest(
    List<AudioPlayerTrack>? Playlist,
    Func<int> GetCurrentTrackIndex,
    string TrackUri,
    string TrackUrl,
    Func<bool, Task> PlayCurrentTrackAsync,
    Func<bool> GetIsManualNavigationPending,
    Func<bool> GetIsAlarm,
    Func<string, bool, Task> ShowPlaybackErrorInModalKeepSessionAsync,
    Func<int, bool> IsPlaybackEstablishedForTrack);
