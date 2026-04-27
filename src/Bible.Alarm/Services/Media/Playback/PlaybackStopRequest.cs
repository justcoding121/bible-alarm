#nullable enable

using System;
using System.Threading;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Parameters for <see cref="PlaybackStopHandler.StopAsync"/>.
/// </summary>
public readonly record struct PlaybackStopRequest(
    int? ScheduleIdToSave,
    TrackMetadata? TrackMetadataToMark,
    bool SkipMarkAsPlayed,
    bool SkipSaveLastPlayed,
    CancellationTokenSource? PreparationCancellationTokenSource,
    Action ResetState,
    Action StopProgressTimer,
    bool SkipDispatchStopped = false);
