using Android.Graphics;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Platforms.Android.Services.Media.Session;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Media;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Media3 implementation of the playback seam. Not registered in DI until cutover.
/// Writes into <see cref="SessionStatePlayer"/> instead of MediaSessionCompat.
/// </summary>
public sealed class Media3MediaSessionManager : IMediaSessionManager
{
    private static readonly ILogger logger = Log.ForContext<Media3MediaSessionManager>();
    private ProxyPlaybackKind? lastKind;
    private long? lastPositionMs;
    private long? lastDurationMs;

    public bool IsAvailable => true;

    public SessionPlaybackSnapshot Snapshot
    {
        get
        {
            var model = SessionStatePlayer.Instance.ReadModel();
            return new SessionPlaybackSnapshot(
                MapKind(model.Kind),
                DisplayedPosition(model),
                model.DurationMs,
                model.CanPlayNext,
                model.CanPlayPrevious,
                model.Title,
                model.Artist,
                model.Album,
                model.ArtworkUrl,
                model.ScheduleId,
                model.IsActive,
                model.ErrorMessage);
        }
    }

    public void UpdatePlaybackState(SessionPlaybackState state, long position = 0, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        var kind = MapState(state);
        if (kind == lastKind && position == lastPositionMs)
        {
            return;
        }

        SessionStatePlayer.Instance.Apply(current => current with
        {
            Kind = kind,
            PositionMs = position,
            CanPlayNext = canPlayNext,
            CanPlayPrevious = canPlayPrevious,
            ErrorMessage = kind == ProxyPlaybackKind.Error ? current.ErrorMessage : null,
        });
        lastKind = kind;
        lastPositionMs = position;
    }

    public void UpdatePlaybackPosition(TimeSpan position, TimeSpan duration, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        var model = SessionStatePlayer.Instance.ReadModel();
        if (model.Kind is not (ProxyPlaybackKind.Playing or ProxyPlaybackKind.Buffering or ProxyPlaybackKind.Paused))
        {
            return;
        }

        var positionMs = (long)position.TotalMilliseconds;
        var durationMs = (long)duration.TotalMilliseconds;
        if (model.Kind == lastKind && positionMs == lastPositionMs && (durationMs <= 0 || durationMs == lastDurationMs))
        {
            return;
        }

        SessionStatePlayer.Instance.Apply(current => current with
        {
            PositionMs = positionMs,
            DurationMs = durationMs > 0 ? durationMs : current.DurationMs,
            CanPlayNext = canPlayNext,
            CanPlayPrevious = canPlayPrevious,
        });
        lastKind = model.Kind;
        lastPositionMs = positionMs;
        if (durationMs > 0)
        {
            lastDurationMs = durationMs;
        }
    }

    public void UpdateMetadata(string title, string artist, string? album = null, int? scheduleId = null, string? artworkUrl = null, TimeSpan? duration = null)
    {
        var current = SessionStatePlayer.Instance.ReadModel();
        var scheduleChanged = scheduleId is > 0 && scheduleId != current.ScheduleId;
        var durationMs = ResolveDuration(current, duration, scheduleChanged);
        var artwork = ResolveArtwork(current, artworkUrl, scheduleChanged);
        if (!scheduleChanged
            && title == current.Title
            && artist == current.Artist
            && album == current.Album
            && artworkUrl == current.ArtworkUrl
            && durationMs == current.DurationMs)
        {
            logger.Debug("Skipping redundant Media3 metadata update");
            return;
        }

        SessionStatePlayer.Instance.Apply(model => model with
        {
            Title = title,
            Artist = artist,
            Album = album,
            ArtworkUrl = artworkUrl,
            Artwork = artwork,
            ScheduleId = scheduleId is > 0 ? scheduleId : model.ScheduleId,
            DurationMs = durationMs,
        });
        lastDurationMs = durationMs > 0 ? durationMs : null;
    }

    public void SetBufferingStateOnly()
    {
        SessionStatePlayer.Instance.ShowBuffering();
        lastKind = ProxyPlaybackKind.Buffering;
    }

    public void UpdatePlaybackStateForStop()
    {
        SessionStatePlayer.Instance.Apply(current => current with
        {
            Kind = ProxyPlaybackKind.Paused,
            PositionMs = 0,
        });
        lastKind = ProxyPlaybackKind.Paused;
        lastPositionMs = 0;
    }

    public void SetPlaybackStatus(PlayStatus status, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        if (status is PlayStatus.Stopped or PlayStatus.Ended)
        {
            UpdatePlaybackStateForStop();
            SetActive(true);
            lastDurationMs = null;
            return;
        }

        var position = SessionStatePlayer.Instance.ReadModel().PositionMs;
        var kind = status switch
        {
            PlayStatus.Playing => ProxyPlaybackKind.Playing,
            PlayStatus.Paused => ProxyPlaybackKind.Paused,
            PlayStatus.Loading => ProxyPlaybackKind.Buffering,
            PlayStatus.Failed => ProxyPlaybackKind.Error,
            _ => ProxyPlaybackKind.None,
        };
        UpdatePlaybackState(MapKind(kind), position, canPlayNext, canPlayPrevious);
        if (status == PlayStatus.Playing)
        {
            SetActive(true);
        }
    }

    public void SetErrorState(string? errorMessage, bool canPlayNext = false, bool canPlayPrevious = false)
    {
        var position = SessionStatePlayer.Instance.ReadModel().PositionMs;
        var message = string.IsNullOrEmpty(errorMessage) ? "Playback failed. Tap Play to retry." : errorMessage;
        SessionStatePlayer.Instance.Apply(current => current with
        {
            Kind = ProxyPlaybackKind.Error,
            PositionMs = position,
            CanPlayNext = canPlayNext,
            CanPlayPrevious = canPlayPrevious,
            ErrorMessage = message,
        });
        lastKind = ProxyPlaybackKind.Error;
        lastPositionMs = position;
    }

    public void SetActive(bool active)
    {
        SessionStatePlayer.Instance.Apply(current => current with { IsActive = active });
        BibleAlarmMediaLibraryService.ApplySessionActive(active);
    }

    public void UpdateDuration(TimeSpan duration)
    {
        var durationMs = (long)duration.TotalMilliseconds;
        if (durationMs <= 0 || durationMs == lastDurationMs)
        {
            return;
        }

        SessionStatePlayer.Instance.Apply(current => current with { DurationMs = durationMs });
        lastDurationMs = durationMs;
    }

    public void SetTrackedDuration(long durationMs)
    {
        lastDurationMs = durationMs > 0 ? durationMs : null;
    }

    private static long ResolveDuration(ProxyModel current, TimeSpan? duration, bool scheduleChanged)
    {
        if (duration is { } value && value > TimeSpan.Zero)
        {
            return (long)value.TotalMilliseconds;
        }

        if (duration.HasValue || !scheduleChanged)
        {
            return Math.Max(current.DurationMs, 0);
        }

        return 0;
    }

    private static byte[]? ResolveArtwork(ProxyModel current, string? artworkUrl, bool scheduleChanged)
    {
        if (!string.IsNullOrEmpty(artworkUrl))
        {
            var loaded = LoadArtwork(artworkUrl);
            if (loaded != null)
            {
                return loaded;
            }
        }

        if (scheduleChanged)
        {
            return null;
        }

        return current.Artwork;
    }

    private static byte[]? LoadArtwork(string artworkUrl)
    {
        try
        {
            var artwork = ServiceProviderManager.GetService<IAndroidArtworkService>()?.LoadArtworkBitmap(artworkUrl);
            if (artwork == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            artwork.Compress(Bitmap.CompressFormat.Png!, 100, stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Media3 artwork load failed for {ArtworkUrl}", artworkUrl);
            return null;
        }
    }

    private static long DisplayedPosition(ProxyModel model)
        => model.PositionMs;

    private static SessionPlaybackState MapKind(ProxyPlaybackKind kind) => kind switch
    {
        ProxyPlaybackKind.Playing => SessionPlaybackState.Playing,
        ProxyPlaybackKind.Buffering => SessionPlaybackState.Buffering,
        ProxyPlaybackKind.Paused => SessionPlaybackState.Paused,
        ProxyPlaybackKind.Error => SessionPlaybackState.Error,
        _ => SessionPlaybackState.None,
    };

    private static ProxyPlaybackKind MapState(SessionPlaybackState state) => state switch
    {
        SessionPlaybackState.Playing => ProxyPlaybackKind.Playing,
        SessionPlaybackState.Buffering => ProxyPlaybackKind.Buffering,
        SessionPlaybackState.Paused => ProxyPlaybackKind.Paused,
        SessionPlaybackState.Error => ProxyPlaybackKind.Error,
        _ => ProxyPlaybackKind.None,
    };
}
