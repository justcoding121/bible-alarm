#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackFailureHandler.HandlePlaybackFailureAsync"/>.
/// </summary>
public readonly record struct PlaybackHandleFailureRequest(
    bool IsAlarm,
    int? CurrentScheduleId,
    Func<Task> ResetAsync,
    Action<List<AudioPlayerTrack>> SetPlaylist,
    Action<int> SetCurrentTrackIndex,
    Func<bool, Task> PlayCurrentTrackAsync);
