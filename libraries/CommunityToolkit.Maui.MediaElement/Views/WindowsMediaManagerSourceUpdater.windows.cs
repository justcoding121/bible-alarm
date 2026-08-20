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
using WindowsMediaElement = Windows.Media.Playback.MediaPlayer;
using WinMediaSource = Windows.Media.Core.MediaSource;

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
        if (playerElement is not null)
        {
            await dispatcher.DispatchAsync(() => playerElement.PosterSource = new BitmapImage());
        }

        if (mediaElement.Source is null)
        {
            ClearWindowsMediaWhenSourceNull(mediaElement, mediaPlayer, playerElement);
            return;
        }

        mediaElement.Position = TimeSpan.Zero;
        mediaElement.Duration = TimeSpan.Zero;

        switch (mediaElement.Source)
        {
            case UriMediaSource uriMediaSource:
                await ApplyUriMediaSourceAsync(mediaElement, logger, mediaPlayer, playerElement, uriMediaSource);
                break;

            case FileMediaSource fileMediaSource:
                await ApplyFileMediaSourceAsync(mediaElement, logger, mediaPlayer, playerElement, fileMediaSource);
                break;

            case ResourceMediaSource resourceMediaSource:
                ApplyResourceMediaSource(mediaElement, logger, mediaPlayer, playerElement, resourceMediaSource);
                break;
        }
    }

    private static void ClearWindowsMediaWhenSourceNull(
        IMediaElement mediaElement,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement)
    {
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
    }

    private static async ValueTask ApplyUriMediaSourceAsync(
        IMediaElement mediaElement,
        ILogger logger,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement,
        UriMediaSource uriMediaSource)
    {
        var uri = uriMediaSource.Uri?.AbsoluteUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return;
        }

        var source = WinMediaSource.CreateFromUri(new Uri(uri));
        var playbackItem = new MediaPlaybackItem(source);

        await ApplyPlaybackItemMetadataAsync(playbackItem, mediaElement, logger);

        AssignPlaybackItemToWindowsSurface(mediaElement, mediaPlayer, playerElement, playbackItem);
    }

    private static async ValueTask ApplyFileMediaSourceAsync(
        IMediaElement mediaElement,
        ILogger logger,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement,
        FileMediaSource fileMediaSource)
    {
        var filename = fileMediaSource.Path;
        if (string.IsNullOrWhiteSpace(filename))
        {
            return;
        }

        StorageFile storageFile = await StorageFile.GetFileFromPathAsync(filename);
        var source = WinMediaSource.CreateFromStorageFile(storageFile);
        var playbackItem = new MediaPlaybackItem(source);

        await ApplyPlaybackItemMetadataAsync(playbackItem, mediaElement, logger);

        AssignPlaybackItemToWindowsSurface(mediaElement, mediaPlayer, playerElement, playbackItem);
    }

    private static void ApplyResourceMediaSource(
        IMediaElement mediaElement,
        ILogger logger,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement,
        ResourceMediaSource resourceMediaSource)
    {
        if (string.IsNullOrWhiteSpace(resourceMediaSource.Path))
        {
            logger.LogInformation("ResourceMediaSource Path is null or empty");
            return;
        }

        string path = GetFullAppPackageFilePath(resourceMediaSource.Path);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var source = WinMediaSource.CreateFromUri(new Uri(path));
        AssignUriMediaSourceToWindowsSurface(mediaElement, mediaPlayer, playerElement, source);
    }

    private static void AssignPlaybackItemToWindowsSurface(
        IMediaElement mediaElement,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement,
        MediaPlaybackItem playbackItem)
    {
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

    private static void AssignUriMediaSourceToWindowsSurface(
        IMediaElement mediaElement,
        WindowsMediaElement mediaPlayer,
        PlatformMediaElement? playerElement,
        WinMediaSource source)
    {
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
    }

    private static async ValueTask ApplyPlaybackItemMetadataAsync(MediaPlaybackItem playbackItem, IMediaElement mediaElement, ILogger logger)
    {
        try
        {
            var displayProps = playbackItem.GetDisplayProperties();
            displayProps.Type = MediaPlaybackType.Music; // Important for media-style display

            if (!string.IsNullOrWhiteSpace(mediaElement.MetadataTitle))
            {
                displayProps.MusicProperties.Title = mediaElement.MetadataTitle;
            }

            if (!string.IsNullOrWhiteSpace(mediaElement.MetadataArtist))
            {
                displayProps.MusicProperties.Artist = mediaElement.MetadataArtist;
            }

            if (!string.IsNullOrWhiteSpace(mediaElement.MetadataArtworkUrl))
            {
                try
                {
                    if (Uri.TryCreate(mediaElement.MetadataArtworkUrl, UriKind.Absolute, out var artworkUri))
                    {
                        displayProps.Thumbnail = RandomAccessStreamReference.CreateFromUri(artworkUri);
                    }
                    else if (System.IO.File.Exists(mediaElement.MetadataArtworkUrl))
                    {
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
