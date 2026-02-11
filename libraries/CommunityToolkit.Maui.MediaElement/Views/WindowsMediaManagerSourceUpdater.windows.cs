#nullable enable

using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.MediaSource;
using CommunityToolkit.Maui.Primitives;
using CommunityToolkit.Maui.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using WinMediaSource = Windows.Media.Core.MediaSource;
using WindowsMediaElement = Windows.Media.Playback.MediaPlayer;

namespace CommunityToolkit.Maui.Views;

internal static class WindowsMediaManagerSourceUpdater
{
    internal static async ValueTask UpdateSourceAsync(
        IMediaElement mediaElement,
        IDispatcher dispatcher,
        ILogger logger,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement)
    {
        // Update poster source only if we have a Player (UI mode)
        if (playerElement is not null)
        {
            await dispatcher.DispatchAsync(() => playerElement.PosterSource = new BitmapImage());
        }

        if (mediaElement.Source is null)
        {
            // In headless mode, we set source directly on MediaPlayer
            // In UI mode, we set source on MediaPlayerElement
            if (playerElement is not null)
            {
                playerElement.Source = null;
            }
            else
            {
                mediaPlayer.Source = null;
            }

            mediaElement.MediaWidth = 0;
            mediaElement.MediaHeight = 0;
            mediaElement.CurrentStateChanged(MediaElementState.None);
            return;
        }

        mediaElement.Position = TimeSpan.Zero;
        mediaElement.Duration = TimeSpan.Zero;

        switch (mediaElement.Source)
        {
            case UriMediaSource uriMediaSource:
                await UpdateFromUriAsync(uriMediaSource);
                break;

            case FileMediaSource fileMediaSource:
                await UpdateFromFileAsync(fileMediaSource);
                break;

            case ResourceMediaSource resourceMediaSource:
                await UpdateFromResourceAsync(resourceMediaSource);
                break;
        }

        async ValueTask UpdateFromUriAsync(UriMediaSource uriMediaSource)
        {
            var uri = uriMediaSource.Uri?.AbsoluteUri;
            if (string.IsNullOrWhiteSpace(uri))
            {
                return;
            }

            var source = WinMediaSource.CreateFromUri(new Uri(uri));
            var playbackItem = new MediaPlaybackItem(source);

            // Set metadata on MediaPlaybackItem for SMTC integration
            await ApplyPlaybackItemMetadataAsync(playbackItem, mediaElement, logger);

            if (playerElement is not null)
            {
                playerElement.AutoPlay = mediaElement.ShouldAutoPlay;
                playerElement.Source = playbackItem;
            }
            else
            {
                mediaPlayer.Source = playbackItem;
                if (mediaElement.ShouldAutoPlay)
                {
                    mediaPlayer.Play();
                }
            }
        }

        async ValueTask UpdateFromFileAsync(FileMediaSource fileMediaSource)
        {
            var filename = fileMediaSource.Path;
            if (string.IsNullOrWhiteSpace(filename))
            {
                return;
            }

            StorageFile storageFile = await StorageFile.GetFileFromPathAsync(filename);
            var source = WinMediaSource.CreateFromStorageFile(storageFile);
            var playbackItem = new MediaPlaybackItem(source);

            // Set metadata on MediaPlaybackItem for SMTC integration
            await ApplyPlaybackItemMetadataAsync(playbackItem, mediaElement, logger);

            if (playerElement is not null)
            {
                playerElement.AutoPlay = mediaElement.ShouldAutoPlay;
                playerElement.Source = playbackItem;
            }
            else
            {
                mediaPlayer.Source = playbackItem;
                if (mediaElement.ShouldAutoPlay)
                {
                    mediaPlayer.Play();
                }
            }
        }

        ValueTask UpdateFromResourceAsync(ResourceMediaSource resourceMediaSource)
        {
            if (string.IsNullOrWhiteSpace(resourceMediaSource.Path))
            {
                logger.LogInformation("ResourceMediaSource Path is null or empty");
                return ValueTask.CompletedTask;
            }

            string path = GetFullAppPackageFilePath(resourceMediaSource.Path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return ValueTask.CompletedTask;
            }

            var source = WinMediaSource.CreateFromUri(new Uri(path));
            if (playerElement is not null)
            {
                playerElement.AutoPlay = mediaElement.ShouldAutoPlay;
                playerElement.Source = source;
            }
            else
            {
                mediaPlayer.Source = source;
                if (mediaElement.ShouldAutoPlay)
                {
                    mediaPlayer.Play();
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    private static async ValueTask ApplyPlaybackItemMetadataAsync(MediaPlaybackItem playbackItem, IMediaElement mediaElement, ILogger logger)
    {
        try
        {
            var displayProps = playbackItem.GetDisplayProperties();
            displayProps.Type = MediaPlaybackType.Music; // Important for media-style display

            // Set metadata from MediaElement properties
            if (!string.IsNullOrWhiteSpace(mediaElement.MetadataTitle))
            {
                displayProps.MusicProperties.Title = mediaElement.MetadataTitle;
            }

            if (!string.IsNullOrWhiteSpace(mediaElement.MetadataArtist))
            {
                displayProps.MusicProperties.Artist = mediaElement.MetadataArtist;
            }

            // Set artwork if available
            if (!string.IsNullOrWhiteSpace(mediaElement.MetadataArtworkUrl))
            {
                try
                {
                    if (Uri.TryCreate(mediaElement.MetadataArtworkUrl, UriKind.Absolute, out var artworkUri))
                    {
                        // For HTTP/HTTPS URIs
                        displayProps.Thumbnail = RandomAccessStreamReference.CreateFromUri(artworkUri);
                    }
                    else if (System.IO.File.Exists(mediaElement.MetadataArtworkUrl))
                    {
                        // For local file paths
                        var storageFile = await StorageFile.GetFileFromPathAsync(mediaElement.MetadataArtworkUrl);
                        displayProps.Thumbnail = RandomAccessStreamReference.CreateFromFile(storageFile);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to set artwork thumbnail for MediaPlaybackItem");
                }
            }

            playbackItem.ApplyDisplayProperties(displayProps);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set metadata on MediaPlaybackItem");
        }
    }

    private static string GetFullAppPackageFilePath(in string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);

        var normalizedFilename = NormalizePath(filename);
        return Path.Combine(AppPackageService.FullAppPackageFilePath, normalizedFilename);

        static string NormalizePath(string filename) => filename.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}

