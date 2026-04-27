#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackEventHandler.HandleMediaEndedAsync"/>.
/// </summary>
public readonly record struct PlaybackMediaEndedRequest(
    List<AudioPlayerTrack>? Playlist,
    Func<int> GetCurrentTrackIndex,
    Action<int> SetCurrentTrackIndex,
    int? CurrentScheduleId,
    bool IsIndefinitePlayback,
    Func<Task<bool>> TryAppendNextTrackAsync,
    Func<bool, Task> PlayCurrentTrackAsync,
    Func<bool, Task> StopAsyncInternal,
    Func<bool> GetIsManualNavigationPending,
    Func<bool> GetIsAlarm,
    Func<string, bool, Task> ShowPlaybackErrorInModalKeepSessionAsync);
