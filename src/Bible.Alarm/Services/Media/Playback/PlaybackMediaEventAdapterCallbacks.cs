#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Callbacks wired into <see cref="PlaybackMediaEventAdapter"/> from <see cref="PlaybackService"/> (reduces constructor parameter count).
/// </summary>
public readonly record struct PlaybackMediaEventAdapterCallbacks(
    Func<List<AudioPlayerTrack>?> GetPlaylist,
    Func<int> GetCurrentTrackIndex,
    Action<int> SetCurrentTrackIndex,
    Func<int?> GetCurrentScheduleId,
    Func<bool> GetIsIndefinitePlayback,
    Func<Task<bool>> TryAppendNextTrackAsync,
    Func<bool, Task> PlayCurrentTrackAsync,
    Func<bool, Task> StopAsyncInternal,
    Func<bool> GetIsManualNavigationPending,
    Func<bool> GetIsAlarm,
    Func<string, bool, Task> ShowPlaybackErrorInModalKeepSessionAsync,
    Func<int, bool> IsPlaybackEstablishedForTrack);
