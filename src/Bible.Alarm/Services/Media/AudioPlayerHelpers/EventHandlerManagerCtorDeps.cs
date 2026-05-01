#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media.AudioPlayerHelpers;

public sealed record EventHandlerManagerDeps(
    ILogger Logger,
    AudioPlayerStateManager StateManager,
    AudioPlayerMetadataHandler MetadataHandler,
    AudioPlayerPositionTracker PositionTracker);

public sealed record EventHandlerManagerCallbacks(
    Func<TimeSpan?> GetCurrentPosition,
    Func<TimeSpan> GetDuration,
    Action<PlayStatus> SetStatus,
    Action<EventArgs>? OnMediaEnded,
    Action<EventArgs>? OnMediaFailed,
    Func<TaskCompletionSource<bool>?> GetMediaOpenedCompletionSource,
    Func<AudioPlayerTrack?> GetCurrentTrack);
