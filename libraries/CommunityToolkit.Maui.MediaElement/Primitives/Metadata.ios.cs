using AVFoundation;
using CommunityToolkit.Maui.Interfaces;
using Foundation;
using MediaPlayer;
using UIKit;

namespace CommunityToolkit.Maui.Primitives;

sealed class Metadata
{
    static readonly UIImage defaultUiImage = new();
    static readonly MPNowPlayingInfo nowPlayingInfoDefault = new()
    {
        AlbumTitle = string.Empty,
        Title = string.Empty,
        Artist = string.Empty,
        PlaybackDuration = 0,
        IsLiveStream = false,
        PlaybackRate = 0,
        ElapsedPlaybackTime = 0,
        Artwork = new(boundsSize: new(0, 0), requestHandler: _ => defaultUiImage)
    };

    private MPMediaItemArtwork? lastArtwork;
    private UIImage? cachedArtworkImage;
    private string? cachedArtworkUri;

    // Remote command handlers (play/pause/toggle/seek) are NOT registered here.
    // The app manages all MPRemoteCommandCenter handlers via iOSRemoteCommandCenterManager,
    // which includes CarPlay auto-play suppression and Fluxor state awareness.
    // Registering a second set of handlers here would bypass that logic and cause
    // direct AVPlayer.Play() calls that circumvent the app's playback pipeline.

    public Metadata(PlatformMediaElement player)
    {
    }

    /// <summary>
    /// The metadata for the currently playing media.
    /// </summary>
    public MPNowPlayingInfo NowPlayingInfo { get; } = new();


    /// <summary>
    /// Clears the metadata for the currently playing media.
    /// </summary>
    public static void ClearNowPlaying()
    {
    }

    /// <summary>
    /// Sets the data for the currently playing media from the media element.
    /// </summary>
    /// <param name="playerItem"></param>
    /// <param name="mediaElement"></param>
    public void SetMetadata(AVPlayerItem? playerItem, IMediaElement? mediaElement)
    {
        if (mediaElement is null)
        {
            ClearNowPlaying();
            return;
        }

        NowPlayingInfo.Title = mediaElement.MetadataTitle;
        NowPlayingInfo.Artist = mediaElement.MetadataArtist;
        NowPlayingInfo.PlaybackDuration = playerItem?.Duration.Seconds ?? 0;
        NowPlayingInfo.IsLiveStream = false;
        NowPlayingInfo.PlaybackRate = mediaElement.Speed;
        NowPlayingInfo.ElapsedPlaybackTime = playerItem?.CurrentTime.Seconds ?? 0;

        var artworkUrl = mediaElement.MetadataArtworkUrl;

        var oldArtwork = lastArtwork;
        var newArtwork = new MPMediaItemArtwork(boundsSize: new(320, 240), requestHandler: _ => GetOrLoadCachedImage(artworkUrl));
        lastArtwork = newArtwork;
        NowPlayingInfo.Artwork = newArtwork;
        MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = NowPlayingInfo;

        if (oldArtwork is not null)
        {
            GC.SuppressFinalize(oldArtwork);
        }
    }

    private UIImage GetOrLoadCachedImage(string? imageUri)
    {
        if (string.IsNullOrEmpty(imageUri))
        {
            return defaultUiImage;
        }

        if (cachedArtworkUri == imageUri && cachedArtworkImage is not null)
        {
            return cachedArtworkImage;
        }

        var oldCachedImage = cachedArtworkImage;

        try
        {
            if (imageUri.StartsWith(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                var url = new NSUrl(imageUri);
                var data = NSData.FromUrl(url);
                cachedArtworkImage = data is not null ? UIImage.LoadFromData(data) ?? defaultUiImage : defaultUiImage;
                GC.SuppressFinalize(url);
                if (data is not null)
                {
                    GC.SuppressFinalize(data);
                }
            }
            else
            {
                cachedArtworkImage = defaultUiImage;
            }
        }
        catch
        {
            cachedArtworkImage = defaultUiImage;
        }

        cachedArtworkUri = imageUri;

        if (oldCachedImage is not null && oldCachedImage != defaultUiImage)
        {
            GC.SuppressFinalize(oldCachedImage);
        }

        return cachedArtworkImage;
    }

    public void Cleanup()
    {
        if (lastArtwork is not null)
        {
            GC.SuppressFinalize(lastArtwork);
            lastArtwork = null;
        }

        if (cachedArtworkImage is not null && cachedArtworkImage != defaultUiImage)
        {
            GC.SuppressFinalize(cachedArtworkImage);
            cachedArtworkImage = null;
        }

        cachedArtworkUri = null;

        MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null!;
    }
}