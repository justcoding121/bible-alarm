#nullable enable

using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Util;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.MediaSource;
using CommunityToolkit.Maui.Primitives;
using Java.Lang;
using Microsoft.Extensions.Logging;
using MediaMetadata = AndroidX.Media3.Common.MediaMetadata;
using Object = Java.Lang.Object;

namespace CommunityToolkit.Maui.Views;

internal static class AndroidMediaManagerSourceUpdater
{
    internal static async ValueTask<(CancellationTokenSource? CancellationTokenSource, MediaItem.Builder? MediaItem)> UpdateSourceAsync(
        IMediaElement mediaElement,
        PlatformMediaElement player,
        PlayerView? playerView,
        CancellationTokenSource? cancellationTokenSource,
        MediaItem.Builder? mediaItem,
        Action updateNotifications,
        ILogger logger)
    {
        if (mediaElement.Source is null)
        {
            player.ClearMediaItems();
            mediaElement.Duration = TimeSpan.Zero;
            mediaElement.CurrentStateChanged(MediaElementState.None);
            return (cancellationTokenSource, mediaItem);
        }

        mediaElement.CurrentStateChanged(MediaElementState.Opening);
        player.PlayWhenReady = mediaElement.ShouldAutoPlay;
        cancellationTokenSource ??= new CancellationTokenSource();

        // ConfigureAwait(true) is required to prevent crash on startup (preserve existing behavior)
        mediaItem = await SetPlayerData(mediaElement, playerView, mediaItem, cancellationTokenSource.Token).ConfigureAwait(true);
        var item = mediaItem?.Build();

        // Bible Alarm manages the ExoPlayer playlist directly via SetSourceWithDummyQueue
        // (3-item queue with dummy prev/next for system media controls).
        // Calling SetMediaItem here would replace that queue with a single item,
        // causing a Media3 MediaSession IllegalStateException when the stale
        // currentMediaItemIndex (from the old 3-item queue) exceeds the new 1-item timeline.

        if (item is not null && player.PlayerError is null)
        {
            mediaElement.MediaOpened();
            updateNotifications();
        }

        return (cancellationTokenSource, mediaItem);
    }

    private static async Task<MediaItem.Builder?> SetPlayerData(
        IMediaElement mediaElement,
        PlayerView? playerView,
        MediaItem.Builder? mediaItem,
        CancellationToken cancellationToken = default)
    {
        if (mediaElement.Source is null)
        {
            return null;
        }

        switch (mediaElement.Source)
        {
            case UriMediaSource uriMediaSource:
                {
                    var uri = uriMediaSource.Uri;
                    if (!string.IsNullOrWhiteSpace(uri?.AbsoluteUri))
                    {
                        return await CreateMediaItem(mediaElement, uri.AbsoluteUri, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }
            case FileMediaSource fileMediaSource:
                {
                    var filePath = fileMediaSource.Path;
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        return await CreateMediaItem(mediaElement, filePath, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }
            case ResourceMediaSource resourceMediaSource:
                {
                    var package = playerView?.Context?.PackageName ?? "";
                    var path = resourceMediaSource.Path;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var assetFilePath = $"asset://{package}{Path.PathSeparator}{path}";
                        return await CreateMediaItem(mediaElement, assetFilePath, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }
            default:
                throw new NotSupportedException($"{mediaElement.Source.GetType().FullName} is not yet supported for {nameof(MediaElement.Source)}");
        }

        return mediaItem;
    }

    private static async Task<MediaItem.Builder> CreateMediaItem(IMediaElement mediaElement, string url, CancellationToken cancellationToken = default)
    {
        MediaMetadata.Builder mediaMetaData = new();
        mediaMetaData.SetArtist(mediaElement.MetadataArtist);
        mediaMetaData.SetTitle(mediaElement.MetadataTitle);

        var data = await AndroidArtworkBytesFetcher.GetBytesFromMetadataArtworkUrl(mediaElement.MetadataArtworkUrl, cancellationToken).ConfigureAwait(true);
        if (data is not null && data.Length > 0)
        {
            mediaMetaData.SetArtworkData(data, (Integer)MediaMetadata.PictureTypeFrontCover);
        }

        var itemBuilder = new MediaItem.Builder();
        itemBuilder.SetUri(url);
        itemBuilder.SetMediaId(url);
        itemBuilder.SetMediaMetadata(mediaMetaData.Build());

        return itemBuilder;
    }
}

