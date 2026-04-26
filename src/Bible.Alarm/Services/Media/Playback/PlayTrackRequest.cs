#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="TrackPlaybackHandler.PlayTrackAsync"/>.
/// </summary>
public readonly record struct PlayTrackRequest(
    AudioPlayerTrack Track,
    int CurrentTrackIndex,
    bool StartFromBeginning,
    int? CurrentScheduleId,
    Func<bool> IsPreparingOrPlaying,
    Func<List<AudioPlayerTrack>?> GetPlaylist,
    Action<bool> SetIsPreparingTrack,
    HashSet<string> PlayedBibleTrackKeys,
    CancellationToken CancellationToken = default);
