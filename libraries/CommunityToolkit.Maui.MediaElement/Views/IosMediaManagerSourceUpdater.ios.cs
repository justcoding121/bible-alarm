#nullable enable

using AVFoundation;
using AVKit;
using CommunityToolkit.Maui.Interfaces;
using CommunityToolkit.Maui.MediaSource;
using CommunityToolkit.Maui.Primitives;
using CoreGraphics;
using CoreMedia;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;
using MediaSourceType = CommunityToolkit.Maui.MediaSource.MediaSource;

namespace CommunityToolkit.Maui.Views;

internal static class IosMediaManagerSourceUpdater
{
    internal readonly record struct UpdateResult(Metadata? MetaData, AVPlayerItem? PlayerItem, IDisposable? CurrentItemErrorObserver);

    internal static UpdateResult UpdateSource(
        IMediaElement mediaElement,
        AVPlayer player,
        AVPlayerViewController? playerViewController,
        Metadata? metaData,
        AVPlayerItem? existingPlayerItem,
        IDisposable? currentItemErrorObserver,
        NSKeyValueObservingOptions valueObserverOptions,
        ILogger logger)
    {
        metaData ??= new(player);
        playerViewController?.ContentOverlayView?.Subviews.FirstOrDefault()?.RemoveFromSuperview();

        var asset = CreateAssetFromMediaSource(mediaElement.Source, logger);

        var playerItem = asset is not null ? new AVPlayerItem(asset) : null;

        if (playerItem is null)
        {
            mediaElement.MediaWidth = 0;
            mediaElement.MediaHeight = 0;
            mediaElement.CurrentStateChanged(MediaElementState.None);
            return new(metaData, null, DisposeAndClear(currentItemErrorObserver));
        }

        (metaData, currentItemErrorObserver) = SetupMetadataAndObservers(metaData, mediaElement, player, playerItem, currentItemErrorObserver, valueObserverOptions, logger);

        if (playerItem.Error is null)
        {
            HandleMediaOpened(mediaElement, player, playerItem, metaData, playerViewController);
        }

        return new(metaData, playerItem, currentItemErrorObserver);
    }

    private static AVAsset? CreateAssetFromMediaSource(MediaSourceType? source, ILogger logger)
    {
        return source switch
        {
            UriMediaSource uriMediaSource => CreateAssetFromUriSource(uriMediaSource),
            FileMediaSource fileMediaSource => CreateAssetFromFileSource(fileMediaSource),
            ResourceMediaSource resourceMediaSource => CreateAssetFromResourceSource(resourceMediaSource, logger),
            _ => null
        };
    }

    private static AVAsset? CreateAssetFromUriSource(UriMediaSource uriMediaSource)
    {
        var uri = uriMediaSource.Uri;
        return !string.IsNullOrWhiteSpace(uri?.AbsoluteUri) ? AVAsset.FromUrl(new NSUrl(uri.AbsoluteUri)) : null;
    }

    private static AVAsset? CreateAssetFromFileSource(FileMediaSource fileMediaSource)
    {
        var uri = fileMediaSource.Path;
        return !string.IsNullOrWhiteSpace(uri) ? AVAsset.FromUrl(NSUrl.CreateFileUrl(uri)) : null;
    }

    private static AVAsset? CreateAssetFromResourceSource(ResourceMediaSource resourceMediaSource, ILogger logger)
    {
        var path = resourceMediaSource.Path;
        if (!string.IsNullOrWhiteSpace(path) && Path.HasExtension(path))
        {
            string directory = Path.GetDirectoryName(path) ?? "";
            string filename = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path)[1..];
            var url = NSBundle.MainBundle.GetUrlForResource(filename, extension, directory);
            return AVAsset.FromUrl(url);
        }

        logger.LogWarning("Invalid file path for ResourceMediaSource.");
        return null;
    }

    private static (Metadata MetaData, IDisposable? CurrentItemErrorObserver) SetupMetadataAndObservers(
        Metadata metaData,
        IMediaElement mediaElement,
        AVPlayer player,
        AVPlayerItem playerItem,
        IDisposable? currentItemErrorObserver,
        NSKeyValueObservingOptions valueObserverOptions,
        ILogger logger)
    {
        currentItemErrorObserver?.Dispose();

        player.ReplaceCurrentItemWithPlayerItem(playerItem);

        currentItemErrorObserver = playerItem.AddObserver("error", valueObserverOptions, _ =>
        {
            if (player.CurrentItem?.Error is null)
            {
                return;
            }

            var message = $"{player.CurrentItem?.Error?.LocalizedDescription} - {player.CurrentItem?.Error?.LocalizedFailureReason}";
            mediaElement.MediaFailed(new MediaFailedEventArgs(message));
            logger.LogError("{LogMessage}", message);
        });

        return (metaData, currentItemErrorObserver);
    }

    private static void HandleMediaOpened(
        IMediaElement mediaElement,
        AVPlayer player,
        AVPlayerItem playerItem,
        Metadata metaData,
        AVPlayerViewController? playerViewController)
    {
        mediaElement.MediaOpened();

        (mediaElement.MediaWidth, mediaElement.MediaHeight) = GetVideoDimensions(playerItem);

        if (mediaElement.ShouldAutoPlay)
        {
            player.Play();
        }

        SetPoster(playerItem, playerViewController, mediaElement);
    }

    private static void SetPoster(AVPlayerItem playerItem, AVPlayerViewController? playerViewController, IMediaElement mediaElement)
    {
        if (ShouldSkipPosterSetting(playerItem) || !CanSetPosterImage(playerViewController, mediaElement))
        {
            return;
        }

        CreateAndAddPosterImage(playerViewController!, mediaElement);
    }

    private static bool ShouldSkipPosterSetting(AVPlayerItem playerItem)
    {
        var videoTrack = playerItem.Asset.TracksWithMediaType(AVMediaTypes.Video.GetConstant() ?? "0").FirstOrDefault();
        if (videoTrack is not null)
        {
            return true;
        }

        // No video track found and no tracks found. This is likely an audio file. So we can't set a poster.
        return playerItem.Asset.Tracks.Length == 0;
    }

    private static bool CanSetPosterImage(AVPlayerViewController? playerViewController, IMediaElement mediaElement)
    {
        return playerViewController?.View is not null &&
               playerViewController.ContentOverlayView is not null &&
               !string.IsNullOrEmpty(mediaElement.MetadataArtworkUrl);
    }

    private static void CreateAndAddPosterImage(AVPlayerViewController playerViewController, IMediaElement mediaElement)
    {
        if (playerViewController.ContentOverlayView is null)
        {
            return;
        }

        var image = UIImage.LoadFromData(NSData.FromUrl(new NSUrl(mediaElement.MetadataArtworkUrl))) ?? new UIImage();
        var imageView = CreatePosterImageView(image);

        playerViewController.ContentOverlayView.AddSubview(imageView);
        SetupPosterConstraints(playerViewController, imageView, image);
    }

    private static UIImageView CreatePosterImageView(UIImage image)
    {
        return new UIImageView(image)
        {
            ContentMode = UIViewContentMode.ScaleAspectFit,
            TranslatesAutoresizingMaskIntoConstraints = false,
            ClipsToBounds = true,
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions
        };
    }

    private static void SetupPosterConstraints(AVPlayerViewController playerViewController, UIImageView imageView, UIImage image)
    {
        if (playerViewController.ContentOverlayView is null)
        {
            return;
        }

        NSLayoutConstraint.ActivateConstraints(
        [
            imageView.CenterXAnchor.ConstraintEqualTo(playerViewController.ContentOverlayView.CenterXAnchor),
            imageView.CenterYAnchor.ConstraintEqualTo(playerViewController.ContentOverlayView.CenterYAnchor),
            imageView.WidthAnchor.ConstraintLessThanOrEqualTo(playerViewController.ContentOverlayView.WidthAnchor),
            imageView.HeightAnchor.ConstraintLessThanOrEqualTo(playerViewController.ContentOverlayView.HeightAnchor),

            // Maintain the aspect ratio
            imageView.WidthAnchor.ConstraintEqualTo(imageView.HeightAnchor, image.Size.Width / image.Size.Height)
        ]);
    }

    private static (int Width, int Height) GetVideoDimensions(AVPlayerItem avPlayerItem)
    {
        var asset = avPlayerItem.Asset;
        var videoTrack = asset.TracksWithMediaType(AVMediaTypes.Video.GetConstant() ?? "0").FirstOrDefault();

        if (videoTrack is not null)
        {
            var size = videoTrack.NaturalSize;
            var preferredTransform = videoTrack.PreferredTransform;

            var transformedSize = CGAffineTransform.CGRectApplyAffineTransform(new CGRect(CGPoint.Empty, size), preferredTransform);
            var width = Math.Abs(transformedSize.Width);
            var height = Math.Abs(transformedSize.Height);
            return ((int)width, (int)height);
        }

        // HLS doesn't have tracks, try to get the dimensions this way
        if (!avPlayerItem.PresentationSize.IsEmpty)
        {
            return ((int)avPlayerItem.PresentationSize.Width, (int)avPlayerItem.PresentationSize.Height);
        }

        return (0, 0);
    }

    private static IDisposable? DisposeAndClear(IDisposable? disposable)
    {
        disposable?.Dispose();
        return null;
    }
}

