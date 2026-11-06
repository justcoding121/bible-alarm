#nullable enable

// Java binding parameter names are not valid C# identifiers
#pragma warning disable S927

using Android.OS;
using Android.Runtime;
using AndroidX.Media3.Common;
using Bible.Alarm.Shared.Media;
using Google.Common.Util.Concurrent;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Process-wide proxy player. The real ExoPlayer stays in the MediaElement fork.
/// This player only publishes the app-defined state that Android Auto and the notification read.
/// </summary>
#pragma warning disable CS0618 // Xamarin Preserve is still required for JNI Media3 callbacks
[Preserve(AllMembers = true)]
#pragma warning restore CS0618
public sealed class SessionStatePlayer : SimpleBasePlayer
{
    private static readonly Lock gate = new();
    private static SessionStatePlayer? instance;

    private readonly Lock modelGate = new();
    private ProxyModel model = ProxyModel.Empty;
    private long anchorElapsedRealtimeMs;

    private SessionStatePlayer()
        : base(Looper.MainLooper ?? throw new InvalidOperationException("Main looper is not available."))
    {
    }

    public static SessionStatePlayer Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            lock (gate)
            {
                if (instance != null)
                {
                    return instance;
                }

                if (Looper.MyLooper() == Looper.MainLooper)
                {
                    instance = new SessionStatePlayer();
                    return instance;
                }

                SessionStatePlayer? created = null;
                using var ready = new ManualResetEventSlim(false);
                new Handler(Looper.MainLooper!).Post(() =>
                {
                    created = new SessionStatePlayer();
                    ready.Set();
                });
                ready.Wait();
                instance = created ?? throw new InvalidOperationException("Proxy player was not created.");
                return instance;
            }
        }
    }

    public int? CurrentScheduleId
    {
        get
        {
            lock (modelGate)
            {
                return model.ScheduleId;
            }
        }
    }

    public bool IsSessionActive
    {
        get
        {
            lock (modelGate)
            {
                return model.IsActive;
            }
        }
    }

    public void Apply(Func<ProxyModel, ProxyModel> update)
    {
        lock (modelGate)
        {
            model = update(model);
            anchorElapsedRealtimeMs = SystemClock.ElapsedRealtime();
        }

        InvalidateOnMainThread();
    }

    public void ShowBuffering()
    {
        Apply(current => current with { Kind = ProxyPlaybackKind.Buffering });
    }

    public ProxyModel ReadModel()
    {
        lock (modelGate)
        {
            return model;
        }
    }

    protected override State? GetState()
    {
        ProxyModel snapshot;
        long anchor;
        lock (modelGate)
        {
            snapshot = model;
            anchor = anchorElapsedRealtimeMs;
        }

        var mapped = ProxyPlaybackMapping.For(snapshot.Kind);
        var metadata = BuildMetadata(snapshot);
        var mediaId = snapshot.ScheduleId is > 0 ? snapshot.ScheduleId.Value.ToString() : string.Empty;
        var item = new MediaItem.Builder()
            .SetMediaId(mediaId)!
            .SetMediaMetadata(metadata)!
            .Build()!;
        var itemData = new MediaItemData.Builder(item)
            .SetMediaItem(item)!
            .SetMediaMetadata(metadata)!
            .SetDurationUs(Math.Max(snapshot.DurationMs, 0) * 1000)!
            .SetIsSeekable(true)!
            .Build()!;
        var playlist = new System.Collections.Generic.List<MediaItemData> { itemData };

        var builder = new State.Builder()
            .SetAvailableCommands(BuildCommands(snapshot))!
            .SetPlaybackState(StateCode(mapped))!
            .SetPlayWhenReady(mapped.PlayWhenReady, InterfaceConsts.PlayWhenReadyChangeReasonUserRequest)!
            .SetPlaylist(playlist)!
            .SetContentPositionMs(DisplayedPosition(snapshot, anchor))!;

        if (mapped.HasPlayerError)
        {
            var message = string.IsNullOrEmpty(snapshot.ErrorMessage)
                ? "Playback failed. Tap Play to retry."
                : snapshot.ErrorMessage;
            builder.SetPlayerError(new PlaybackException(message, null, PlaybackException.ErrorCodeUnspecified));
        }

        return builder.Build();
    }

    protected override IListenableFuture? HandleSetPlayWhenReady(bool playWhenReady)
    {
        if (playWhenReady)
        {
            SessionCommandRouter.Play();
        }
        else
        {
            SessionCommandRouter.Pause();
        }

        return GuavaFutures.ImmediateVoid();
    }

    protected override IListenableFuture? HandleSeek(int mediaItemIndex, long positionMs, int seekCommand)
    {
        _ = mediaItemIndex;
        if (seekCommand == InterfaceConsts.CommandSeekToNext || seekCommand == InterfaceConsts.CommandSeekToNextMediaItem)
        {
            SessionCommandRouter.Next();
        }
        else if (seekCommand == InterfaceConsts.CommandSeekToPrevious || seekCommand == InterfaceConsts.CommandSeekToPreviousMediaItem)
        {
            SessionCommandRouter.Previous();
        }
        else if (seekCommand == InterfaceConsts.CommandSeekForward)
        {
            SessionCommandRouter.SeekForward();
        }
        else if (seekCommand == InterfaceConsts.CommandSeekBack)
        {
            SessionCommandRouter.SeekBackward();
        }
        else
        {
            SessionCommandRouter.SeekTo(positionMs);
        }

        return GuavaFutures.ImmediateVoid();
    }

    protected override IListenableFuture? HandleSetMediaItems(System.Collections.Generic.IList<MediaItem>? mediaItems, int startIndex, long startPositionMs)
    {
        _ = startIndex;
        _ = startPositionMs;
        var mediaId = mediaItems is { Count: > 0 } ? mediaItems[0].MediaId : null;
        SessionCommandRouter.PlayFromMediaId(mediaId);
        return GuavaFutures.ImmediateVoid();
    }

    protected override IListenableFuture? HandleStop()
    {
        SessionCommandRouter.Pause();
        return GuavaFutures.ImmediateVoid();
    }

    protected override IListenableFuture? HandlePrepare() => GuavaFutures.ImmediateVoid();

    protected override IListenableFuture? HandleRelease() => GuavaFutures.ImmediateVoid();

    private void InvalidateOnMainThread()
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            InvalidateState();
            return;
        }

        new Handler(Looper.MainLooper!).Post(InvalidateState);
    }

    private static long DisplayedPosition(ProxyModel snapshot, long anchorElapsedRealtimeMs)
    {
        if (snapshot.Kind != ProxyPlaybackKind.Playing || anchorElapsedRealtimeMs <= 0)
        {
            return Math.Max(snapshot.PositionMs, 0);
        }

        var elapsed = Math.Max(0, SystemClock.ElapsedRealtime() - anchorElapsedRealtimeMs);
        var position = snapshot.PositionMs + elapsed;
        if (snapshot.DurationMs > 0)
        {
            position = Math.Min(position, snapshot.DurationMs);
        }

        return position;
    }

    private static int StateCode(ProxyPlaybackMapping mapped) => mapped.StateName switch
    {
        "READY" => InterfaceConsts.StateReady,
        "BUFFERING" => InterfaceConsts.StateBuffering,
        _ => InterfaceConsts.StateIdle,
    };

    private static PlayerCommands BuildCommands(ProxyModel snapshot)
    {
        var commands = new PlayerCommands.Builder()
            .Add(InterfaceConsts.CommandPlayPause)!
            .Add(InterfaceConsts.CommandPrepare)!
            .Add(InterfaceConsts.CommandStop)!
            .Add(InterfaceConsts.CommandGetCurrentMediaItem)!
            .Add(InterfaceConsts.CommandGetMetadata)!
            .Add(InterfaceConsts.CommandGetTimeline)!
            .Add(InterfaceConsts.CommandSeekInCurrentMediaItem)!
            .Add(InterfaceConsts.CommandSeekForward)!
            .Add(InterfaceConsts.CommandSeekBack)!
            .Add(InterfaceConsts.CommandSetMediaItem)!;
        if (snapshot.CanPlayNext)
        {
            commands.Add(InterfaceConsts.CommandSeekToNext);
        }

        if (snapshot.CanPlayPrevious)
        {
            commands.Add(InterfaceConsts.CommandSeekToPrevious);
        }

        return commands.Build()!;
    }

    private static MediaMetadata BuildMetadata(ProxyModel snapshot)
    {
        var builder = new MediaMetadata.Builder()
            .SetTitle(string.IsNullOrEmpty(snapshot.Title) ? "Bible Alarm" : snapshot.Title)!
            .SetArtist(snapshot.Artist ?? string.Empty)!
            .SetAlbumTitle(snapshot.Album ?? string.Empty)!
            .SetDurationMs(Java.Lang.Long.ValueOf(Math.Max(snapshot.DurationMs, 0)))!;
        if (!string.IsNullOrEmpty(snapshot.ArtworkUrl) && global::Android.Net.Uri.Parse(snapshot.ArtworkUrl) is { } artworkUri)
        {
            builder.SetArtworkUri(artworkUri);
        }

        if (snapshot.Artwork is { Length: > 0 })
        {
            builder.SetArtworkData(snapshot.Artwork, (Java.Lang.Integer)MediaMetadata.PictureTypeFrontCover);
        }

        return builder.Build()!;
    }

    private new static class InterfaceConsts
    {
        public const int StateIdle = BasePlayer.InterfaceConsts.StateIdle;
        public const int StateBuffering = BasePlayer.InterfaceConsts.StateBuffering;
        public const int StateReady = BasePlayer.InterfaceConsts.StateReady;
        public const int PlayWhenReadyChangeReasonUserRequest = BasePlayer.InterfaceConsts.PlayWhenReadyChangeReasonUserRequest;
        public const int CommandPlayPause = BasePlayer.InterfaceConsts.CommandPlayPause;
        public const int CommandPrepare = BasePlayer.InterfaceConsts.CommandPrepare;
        public const int CommandStop = BasePlayer.InterfaceConsts.CommandStop;
        public const int CommandGetCurrentMediaItem = BasePlayer.InterfaceConsts.CommandGetCurrentMediaItem;
        public const int CommandGetMetadata = BasePlayer.InterfaceConsts.CommandGetMetadata;
        public const int CommandGetTimeline = BasePlayer.InterfaceConsts.CommandGetTimeline;
        public const int CommandSeekInCurrentMediaItem = BasePlayer.InterfaceConsts.CommandSeekInCurrentMediaItem;
        public const int CommandSeekForward = BasePlayer.InterfaceConsts.CommandSeekForward;
        public const int CommandSeekBack = BasePlayer.InterfaceConsts.CommandSeekBack;
        public const int CommandSetMediaItem = BasePlayer.InterfaceConsts.CommandSetMediaItem;
        public const int CommandSeekToNext = BasePlayer.InterfaceConsts.CommandSeekToNext;
        public const int CommandSeekToNextMediaItem = BasePlayer.InterfaceConsts.CommandSeekToNextMediaItem;
        public const int CommandSeekToPrevious = BasePlayer.InterfaceConsts.CommandSeekToPrevious;
        public const int CommandSeekToPreviousMediaItem = BasePlayer.InterfaceConsts.CommandSeekToPreviousMediaItem;
    }
}

/// <summary>
/// Immutable app-defined state held by <see cref="SessionStatePlayer"/>.
/// </summary>
public sealed record ProxyModel(
    ProxyPlaybackKind Kind,
    long PositionMs,
    long DurationMs,
    bool CanPlayNext,
    bool CanPlayPrevious,
    string? Title,
    string? Artist,
    string? Album,
    string? ArtworkUrl,
    byte[]? Artwork,
    int? ScheduleId,
    bool IsActive,
    string? ErrorMessage)
{
    public static ProxyModel Empty { get; } = new(
        ProxyPlaybackKind.None,
        0,
        0,
        false,
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        null);
}

#pragma warning restore S927
