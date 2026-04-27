#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackFailureHandler.TryPlayFallbackWhenPrepareFailedAsync"/>.
/// </summary>
public readonly record struct PlaybackPrepareFallbackRequest(
    int ScheduleId,
    bool KeepErrorMessage,
    Action<List<AudioPlayerTrack>> SetPlaylist,
    Action<int> SetCurrentTrackIndex,
    Action ClearManuallyVisited,
    Action<List<AudioPlayerTrack>, int> NotifyNavigationChanged,
    Func<bool, Task> PlayCurrentTrackAsync);
