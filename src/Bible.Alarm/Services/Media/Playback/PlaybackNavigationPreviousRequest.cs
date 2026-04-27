#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackNavigationHandler.PlayPreviousAsync"/>.
/// </summary>
public readonly record struct PlaybackNavigationPreviousRequest(
    List<AudioPlayerTrack>? Playlist,
    Func<int> GetCurrentTrackIndex,
    Action<int> SetCurrentTrackIndex,
    int? CurrentScheduleId,
    bool IsIndefinitePlayback,
    Func<Task<bool>> TryPrependPreviousTrackAsync,
    HashSet<int> ManuallyVisitedTrackIndices,
    Func<int, Task> MarkCurrentTrackAsPlayedAsync,
    Func<bool, Task> PlayCurrentTrackAsync,
    Func<Task> HandlePlaybackFailureAsync);
