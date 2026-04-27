#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackNavigationHandler.PlayNextAsync"/>.
/// </summary>
public readonly record struct PlaybackNavigationNextRequest(
    List<AudioPlayerTrack>? Playlist,
    Func<int> GetCurrentTrackIndex,
    Action<int> SetCurrentTrackIndex,
    int? CurrentScheduleId,
    bool IsIndefinitePlayback,
    Func<Task<bool>> TryAppendNextTrackAsync,
    HashSet<int> ManuallyVisitedTrackIndices,
    Func<int, Task> MarkCurrentTrackAsPlayedAsync,
    Func<bool, Task> PlayCurrentTrackAsync,
    Func<Task> StopPlaybackAsync,
    Func<Task> HandlePlaybackFailureAsync);
