using AVFoundation;
using CommunityToolkit.Maui.Interfaces;
using Foundation;
using MediaPlayer;
using UIKit;

namespace CommunityToolkit.Maui.Primitives;

sealed class Metadata
{
    static readonly UIImage defaultUiImage = new();
    private MPMediaItemArtwork? lastArtwork;
    private UIImage? cachedArtworkImage;
    private string? cachedArtworkUri;

    // Remote command handlers (play/pause/toggle/seek) are NOT registered here.
    // The app manages all MPRemoteCommandCenter handlers via IOsRemoteCommandCenterManager,
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
        /*
         * Clearing is handled by callers: SetMetadata resets fields on NowPlayingInfo, and Cleanup sets
         * MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null — there is no extra system API needed here.
         */
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

        _ = oldArtwork;
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
            }
            else
            {
                cachedArtworkImage = defaultUiImage;
            }
        }
        catch (Exception)
        {
            cachedArtworkImage = defaultUiImage;
        }

        cachedArtworkUri = imageUri;

        _ = oldCachedImage;

        return cachedArtworkImage;
    }

    public void Cleanup()
    {
        lastArtwork = null;

        if (cachedArtworkImage is not null && cachedArtworkImage != defaultUiImage)
        {
            cachedArtworkImage = null;
        }

        cachedArtworkUri = null;

        MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null!;
    }
}