#nullable enable

using CommunityToolkit;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media;

namespace CommunityToolkit.Maui.Views;

internal static class WindowsMediaManagerMetadataUpdater
{
    internal static async ValueTask UpdateMetadataAsync(
        Metadata metadata,
        SystemMediaTransportControls systemMediaControls,
        IMediaElement mediaElement,
        IDispatcher dispatcher,
        PlatformMediaElement? playerElement,
        ILogger logger)
    {
        metadata.SetMetadata(mediaElement);

        if (string.IsNullOrEmpty(mediaElement.MetadataArtworkUrl))
        {
            return;
        }

        if (!Uri.TryCreate(mediaElement.MetadataArtworkUrl, UriKind.RelativeOrAbsolute, out var metadataArtworkUri))
        {
            logger.LogError("{MediaElement} unable to update artwork because {MetadataArtworkUrl} is not a valid URI", nameof(MediaElement), nameof(mediaElement.MetadataArtworkUrl));
            return;
        }

        // Update poster source only if we have a Player (UI mode)
        if (playerElement is not null)
        {
            if (dispatcher.IsDispatchRequired)
            {
                await dispatcher.DispatchAsync(() => UpdatePosterSource(playerElement, metadataArtworkUri));
            }
            else
            {
                UpdatePosterSource(playerElement, metadataArtworkUri);
            }
        }

        static void UpdatePosterSource(in PlatformMediaElement player, in Uri metadataArtworkUri)
        {
            player.PosterSource = new BitmapImage(metadataArtworkUri);
        }
    }
}

