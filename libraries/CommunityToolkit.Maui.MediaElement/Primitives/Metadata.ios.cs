using AVFoundation;
using CommunityToolkit.Maui.Interfaces;
using CoreMedia;
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

    readonly PlatformMediaElement player;
    private MPMediaItemArtwork? lastArtwork;
    private UIImage? cachedArtworkImage;
    private string? cachedArtworkUri;
    private NSObject? toggleToken;
    private NSObject? playToken;
    private NSObject? pauseToken;
    private NSObject? seekToken;
    private NSObject? seekBackwardToken;
    private NSObject? seekForwardToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="Metadata"/> class.
    /// </summary>
    /// <param name="player"></param>
    public Metadata(PlatformMediaElement player)
    {
        this.player = player;

        var commandCenter = MPRemoteCommandCenter.Shared;

        commandCenter.TogglePlayPauseCommand.Enabled = true;
        toggleToken = commandCenter.TogglePlayPauseCommand.AddTarget(ToggleCommand);

        commandCenter.PlayCommand.Enabled = true;
        playToken = commandCenter.PlayCommand.AddTarget(PlayCommand);

        commandCenter.PauseCommand.Enabled = true;
        pauseToken = commandCenter.PauseCommand.AddTarget(PauseCommand);

        commandCenter.ChangePlaybackPositionCommand.Enabled = true;
        seekToken = commandCenter.ChangePlaybackPositionCommand.AddTarget(SeekCommand);

        commandCenter.SeekBackwardCommand.Enabled = true;
        seekBackwardToken = commandCenter.SeekBackwardCommand.AddTarget(SeekBackwardCommand);

        commandCenter.SeekForwardCommand.Enabled = false;
        seekForwardToken = commandCenter.SeekForwardCommand.AddTarget(SeekForwardCommand);
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

    /// <summary>
    /// Removes remote command targets and suppresses finalizers on native objects
    /// to prevent SIGSEGV from GC finalizer sending objc_msgSend to freed objects.
    /// </summary>
    public void Cleanup()
    {
        try
        {
            var commandCenter = MPRemoteCommandCenter.Shared;
            RemoveAndSuppressToken(commandCenter.TogglePlayPauseCommand, ref toggleToken);
            RemoveAndSuppressToken(commandCenter.PlayCommand, ref playToken);
            RemoveAndSuppressToken(commandCenter.PauseCommand, ref pauseToken);
            RemoveAndSuppressToken(commandCenter.ChangePlaybackPositionCommand, ref seekToken);
            RemoveAndSuppressToken(commandCenter.SeekBackwardCommand, ref seekBackwardToken);
            RemoveAndSuppressToken(commandCenter.SeekForwardCommand, ref seekForwardToken);
        }
        catch (ObjectDisposedException)
        {
        }

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

    private static void RemoveAndSuppressToken(MPRemoteCommand command, ref NSObject? token)
    {
        if (token is null)
        {
            return;
        }

        try
        {
            command.RemoveTarget(token);
        }
        catch (ObjectDisposedException)
        {
        }

        GC.SuppressFinalize(token);
        token = null;
    }

    MPRemoteCommandHandlerStatus SeekCommand(MPRemoteCommandEvent? commandEvent)
    {
        if (commandEvent is not MPChangePlaybackPositionCommandEvent eventArgs)
        {
            return MPRemoteCommandHandlerStatus.CommandFailed;
        }

        var seekTime = CMTime.FromSeconds(eventArgs.PositionTime, 1);
        player.Seek(seekTime);
        return MPRemoteCommandHandlerStatus.Success;
    }

    MPRemoteCommandHandlerStatus SeekBackwardCommand(MPRemoteCommandEvent? commandEvent)
    {
        if (commandEvent is null)
        {
            return MPRemoteCommandHandlerStatus.CommandFailed;
        }

        var seekTime = player.CurrentTime - CMTime.FromSeconds(10, 1);
        player.Seek(seekTime);
        return MPRemoteCommandHandlerStatus.Success;
    }

    MPRemoteCommandHandlerStatus SeekForwardCommand(MPRemoteCommandEvent? commandEvent)
    {
        if (commandEvent is null)
        {
            return MPRemoteCommandHandlerStatus.CommandFailed;
        }

        var seekTime = player.CurrentTime + CMTime.FromSeconds(10, 1);
        player.Seek(seekTime);
        return MPRemoteCommandHandlerStatus.Success;
    }

    MPRemoteCommandHandlerStatus PlayCommand(MPRemoteCommandEvent? commandEvent)
    {
        if (commandEvent is null)
        {
            return MPRemoteCommandHandlerStatus.CommandFailed;
        }

        player.Play();
        return MPRemoteCommandHandlerStatus.Success;
    }

    MPRemoteCommandHandlerStatus PauseCommand(MPRemoteCommandEvent? commandEvent)
    {
        if (commandEvent is null)
        {
            return MPRemoteCommandHandlerStatus.CommandFailed;
        }

        player.Pause();
        return MPRemoteCommandHandlerStatus.Success;
    }

    MPRemoteCommandHandlerStatus ToggleCommand(MPRemoteCommandEvent? commandEvent)
    {
        if (commandEvent is not null)
        {
            return MPRemoteCommandHandlerStatus.CommandFailed;
        }

        if (player.Rate is 0)
        {
            player.Play();
        }
        else
        {
            player.Pause();
        }

        return MPRemoteCommandHandlerStatus.Success;
    }
}