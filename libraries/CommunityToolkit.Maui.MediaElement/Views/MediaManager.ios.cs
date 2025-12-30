using AVFoundation;
using AVKit;
using CommunityToolkit.Maui.Views;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using Foundation;
using MediaPlayer;
using Microsoft.Extensions.Logging;
using UIKit;

namespace CommunityToolkit.Maui.Core.Views;

public partial class MediaManager : IDisposable
{
	Metadata? metaData;

	// Media would still start playing when Speed was set although ShouldAutoPlay=False
	// This field was added to overcome that.
	bool isInitialSpeedSet;

	/// <summary>
	/// Creates the corresponding platform view of <see cref="MediaElement"/> on iOS and macOS.
	/// </summary>
	/// <returns>The platform native counterpart of <see cref="MediaElement"/>. PlayerViewController is null in headless mode.</returns>
	public (PlatformMediaElement Player, AVPlayerViewController? PlayerViewController) CreatePlatformView()
	{
		CreatePlayer();
		InitializePlayerProperties();
		SetupRemoteControlAndAudio();
		SetupObservers();

		// In headless mode (audio-only), we don't need a PlayerViewController
		// AVPlayer works perfectly without a view controller for audio playback
		PlayerViewController = null;

		return (Player!, PlayerViewController);
	}

	void CreatePlayer()
	{
		Player = new();
		// PlayerViewController is not created in headless mode
		// AVPlayer can work without a view controller for audio-only playback
	}

	void InitializePlayerProperties()
	{
		// Pre-initialize Volume and Muted properties to the player object
		if (Player is null)
		{
			return;
		}

		Player.Muted = MediaElement.ShouldMute;
		var volumeDiff = Math.Abs(Player.Volume - MediaElement.Volume);
		if (volumeDiff > 0.01)
		{
			Player.Volume = (float)MediaElement.Volume;
		}
	}

	void SetupRemoteControlAndAudio()
	{
		UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();

		// Audio session must be configured even in headless mode for playback to work
		var avSession = AVAudioSession.SharedInstance();
		avSession.SetCategory(AVAudioSessionCategory.Playback);
		avSession.SetActive(true);

		// PlayerViewController-specific settings only apply when we have a view controller
		if (PlayerViewController is null)
		{
			return;
		}

#if IOS
		PlayerViewController.UpdatesNowPlayingInfoCenter = false;
#else
		PlayerViewController.UpdatesNowPlayingInfoCenter = true;
#endif
	}

	void SetupObservers()
	{
		AddStatusObservers();
		AddPlayedToEndObserver();
		AddErrorObservers();
	}

	/// <summary>
	/// Releases the managed and unmanaged resources used by the <see cref="MediaManager"/>.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// The default <see cref="NSKeyValueObservingOptions"/> flags used in the iOS and macOS observers.
	/// </summary>
	protected NSKeyValueObservingOptions ValueObserverOptions => NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New;

	/// <summary>
	/// Observer that tracks when an error has occurred in the playback of the current item.
	/// </summary>
	protected IDisposable? CurrentItemErrorObserver { get; set; }

	/// <summary>
	/// Observer that tracks when an error has occurred with media playback.
	/// </summary>
	protected NSObject? ErrorObserver { get; set; }

	/// <summary>
	/// Observer that tracks when the media has failed to play to the end.
	/// </summary>
	protected NSObject? ItemFailedToPlayToEndTimeObserver { get; set; }

	/// <summary>
	/// Observer that tracks when the playback of media has stalled.
	/// </summary>
	protected NSObject? PlaybackStalledObserver { get; set; }

	/// <summary>
	/// Observer that tracks when the media has played to the end.
	/// </summary>
	protected NSObject? PlayedToEndObserver { get; set; }

	/// <summary>
	/// The current media playback item.
	/// </summary>
	protected AVPlayerItem? PlayerItem { get; set; }

	/// <summary>
	/// The <see cref="AVPlayerViewController"/> that hosts the media Player.
	/// </summary>
	protected AVPlayerViewController? PlayerViewController { get; set; }

	/// <summary>
	/// Observer that tracks the playback rate of the media.
	/// </summary>
	protected IDisposable? RateObserver { get; set; }

	/// <summary>
	/// Observer that tracks the status of the media.
	/// </summary>
	protected IDisposable? StatusObserver { get; set; }

	/// <summary>
	/// Observer that tracks the time control status of the media.
	/// </summary>
	protected IDisposable? TimeControlStatusObserver { get; set; }

	/// <summary>
	/// Observer that tracks the volume of the media playback.
	/// </summary>
	protected IDisposable? VolumeObserver { get; set; }

	/// <summary>
	/// Observer that tracks if the audio is muted.
	/// </summary>
	protected IDisposable? MutedObserver { get; set; }

	protected virtual partial void PlatformPlay()
	{
		if (Player?.CurrentTime == PlayerItem?.Duration)
		{
			return;
		}

		Player?.Play();
	}

	protected virtual partial void PlatformPause()
	{
		Player?.Pause();
	}

	protected virtual async partial Task PlatformSeek(TimeSpan position, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		var seekTaskCompletionSource = new TaskCompletionSource();

		if (Player?.CurrentItem is null)
		{
			throw new InvalidOperationException($"{nameof(AVPlayer)}.{nameof(AVPlayer.CurrentItem)} is not yet initialized");
		}

		if (Player.Status is not AVPlayerStatus.ReadyToPlay)
		{
			throw new InvalidOperationException($"{nameof(AVPlayer)}.{nameof(AVPlayer.Status)} must first be set to {AVPlayerStatus.ReadyToPlay}");
		}

		var ranges = Player.CurrentItem.SeekableTimeRanges;
		var seekToTime = new CMTime(Convert.ToInt64(position.TotalMilliseconds), 1000);
		bool seekPerformed = false;

		foreach (var range in ranges.Select(r => r.CMTimeRangeValue))
		{
			if (seekToTime >= range.Start && seekToTime < (range.Start + range.Duration))
			{
				Player.Seek(seekToTime, complete =>
				{
					if (!complete)
					{
						seekTaskCompletionSource.SetException(new InvalidOperationException("Seek Failed"));
						return;
					}

					seekTaskCompletionSource.SetResult();
				});
				seekPerformed = true;
				break;
			}
		}

		// If position is outside all seekable ranges, clamp to the nearest valid position
		if (!seekPerformed && ranges.Length > 0)
		{
			var firstRange = ranges[0].CMTimeRangeValue;
			var lastRange = ranges[ranges.Length - 1].CMTimeRangeValue;
			var lastRangeEnd = lastRange.Start + lastRange.Duration;

			CMTime clampedTime;
			if (seekToTime < firstRange.Start)
			{
				// Position is before first range - seek to start of first range
				clampedTime = firstRange.Start;
			}
			else if (seekToTime >= lastRangeEnd)
			{
				// Position is after last range - seek to end of last range
				clampedTime = lastRangeEnd;
			}
			else
			{
				// Position is between ranges - find the nearest range
				CMTime? nearestStart = null;
				CMTime? nearestEnd = null;
				double minDistance = double.MaxValue;

				foreach (var range in ranges.Select(r => r.CMTimeRangeValue))
				{
					var rangeStart = range.Start;
					var rangeEnd = range.Start + range.Duration;

					if (seekToTime < rangeStart)
					{
						// Calculate distance in seconds using CMTime.Seconds property
						var distance = seekToTime.Seconds - rangeStart.Seconds;
						if (Math.Abs(distance) < Math.Abs(minDistance))
						{
							minDistance = distance;
							nearestStart = rangeStart;
						}
					}
					else if (seekToTime >= rangeEnd)
					{
						// Calculate distance in seconds using CMTime.Seconds property
						var distance = seekToTime.Seconds - rangeEnd.Seconds;
						if (Math.Abs(distance) < Math.Abs(minDistance))
						{
							minDistance = distance;
							nearestEnd = rangeEnd;
						}
					}
				}

				clampedTime = nearestStart ?? nearestEnd ?? firstRange.Start;
			}

			Player.Seek(clampedTime, complete =>
			{
				if (!complete)
				{
					seekTaskCompletionSource.SetException(new InvalidOperationException("Seek Failed"));
					return;
				}

				seekTaskCompletionSource.SetResult();
			});
			seekPerformed = true;
		}

		if (!seekPerformed)
		{
			// No seekable ranges available - this can happen when media is still loading
			// Instead of throwing, just complete the seek task (effectively a no-op)
			// The caller can check the position after if needed
			// This allows playback to start from the beginning if seek isn't possible yet
			seekTaskCompletionSource.SetResult();
		}

		// Wait for seek to complete (or already completed if no ranges available)
		await seekTaskCompletionSource.Task.WaitAsync(token);

		// Always call SeekCompleted to notify listeners, even if seek was a no-op
		MediaElement.SeekCompleted();
	}

	protected virtual partial void PlatformStop()
	{
		// There's no Stop method so pause the video and reset its position
		Player?.Seek(CMTime.Zero);
		Player?.Pause();

		MediaElement.CurrentStateChanged(MediaElementState.Stopped);
	}

	protected virtual partial void PlatformUpdateAspect()
	{
		if (PlayerViewController is null)
		{
			return;
		}

		PlayerViewController.VideoGravity = MediaElement.Aspect switch
		{
			Aspect.Fill => AVLayerVideoGravity.Resize,
			Aspect.AspectFill => AVLayerVideoGravity.ResizeAspectFill,
			_ => AVLayerVideoGravity.ResizeAspect,
		};
	}

	protected virtual partial ValueTask PlatformUpdateSource()
	{
		MediaElement.CurrentStateChanged(MediaElementState.Opening);

		if (Player is null)
		{
			return ValueTask.CompletedTask;
		}

		InitializeMetadataAndClearOverlay();

		var asset = CreateAssetFromMediaSource();

		PlayerItem = asset is not null
			? new AVPlayerItem(asset)
			: null;

		SetupMetadataAndObservers();

		if (PlayerItem is not null && PlayerItem.Error is null)
		{
			HandleMediaOpened();
		}
		else if (PlayerItem is null)
		{
			HandleNoMediaSource();
		}

		return ValueTask.CompletedTask;
	}

	void InitializeMetadataAndClearOverlay()
	{
		if (Player is not null)
		{
			metaData ??= new(Player);
		}
		Metadata.ClearNowPlaying();
		PlayerViewController?.ContentOverlayView?.Subviews.FirstOrDefault()?.RemoveFromSuperview();
	}

	AVAsset? CreateAssetFromMediaSource()
	{
		if (MediaElement.Source is UriMediaSource uriMediaSource)
		{
			return CreateAssetFromUriSource(uriMediaSource);
		}
		else if (MediaElement.Source is FileMediaSource fileMediaSource)
		{
			return CreateAssetFromFileSource(fileMediaSource);
		}
		else if (MediaElement.Source is ResourceMediaSource resourceMediaSource)
		{
			return CreateAssetFromResourceSource(resourceMediaSource);
		}

		return null;
	}

	AVAsset? CreateAssetFromUriSource(UriMediaSource uriMediaSource)
	{
		var uri = uriMediaSource.Uri;
		if (!string.IsNullOrWhiteSpace(uri?.AbsoluteUri))
		{
			return AVAsset.FromUrl(new NSUrl(uri.AbsoluteUri));
		}
		return null;
	}

	AVAsset? CreateAssetFromFileSource(FileMediaSource fileMediaSource)
	{
		var uri = fileMediaSource.Path;
		if (!string.IsNullOrWhiteSpace(uri))
		{
			return AVAsset.FromUrl(NSUrl.CreateFileUrl(uri));
		}
		return null;
	}

	AVAsset? CreateAssetFromResourceSource(ResourceMediaSource resourceMediaSource)
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
		else
		{
			Logger.LogWarning("Invalid file path for ResourceMediaSource.");
		}
		return null;
	}

	void SetupMetadataAndObservers()
	{
		if (metaData is null || Player is null)
		{
			return;
		}

		metaData.SetMetadata(PlayerItem, MediaElement);
		CurrentItemErrorObserver?.Dispose();

		Player.ReplaceCurrentItemWithPlayerItem(PlayerItem);

		CurrentItemErrorObserver = PlayerItem?.AddObserver("error",
			ValueObserverOptions, _ =>
			{
				if (Player.CurrentItem?.Error is null)
				{
					return;
				}

				var message = $"{Player.CurrentItem?.Error?.LocalizedDescription} - " +
							  $"{Player.CurrentItem?.Error?.LocalizedFailureReason}";

				MediaElement.MediaFailed(
					new MediaFailedEventArgs(message));

				Logger.LogError("{LogMessage}", message);
			});
	}

	void HandleMediaOpened()
	{
		MediaElement.MediaOpened();
		if (PlayerItem is not null)
		{
			(MediaElement.MediaWidth, MediaElement.MediaHeight) = GetVideoDimensions(PlayerItem);
		}

		if (Player is not null && MediaElement.ShouldAutoPlay)
		{
			Player.Play();
		}

		SetPoster();
	}

	void HandleNoMediaSource()
	{
		MediaElement.MediaWidth = MediaElement.MediaHeight = 0;
		MediaElement.CurrentStateChanged(MediaElementState.None);
	}

	void SetPoster()
	{
		if (PlayerItem is null || metaData is null)
		{
			return;
		}

		if (ShouldSkipPosterSetting())
		{
			return;
		}

		if (CanSetPosterImage())
		{
			CreateAndAddPosterImage();
		}
	}

	bool ShouldSkipPosterSetting()
	{
		if (PlayerItem is null)
		{
			return true;
		}

		var videoTrack = PlayerItem.Asset.TracksWithMediaType(AVMediaTypes.Video.GetConstant() ?? "0").FirstOrDefault();
		if (videoTrack is not null)
		{
			return true;
		}

		if (PlayerItem.Asset.Tracks.Length == 0)
		{
			// No video track found and no tracks found. This is likely an audio file. So we can't set a poster.
			return true;
		}

		return false;
	}

	bool CanSetPosterImage()
	{
		return PlayerViewController?.View is not null &&
			   PlayerViewController.ContentOverlayView is not null &&
			   !string.IsNullOrEmpty(MediaElement.MetadataArtworkUrl);
	}

	void CreateAndAddPosterImage()
	{
		if (PlayerViewController?.ContentOverlayView is null)
		{
			return;
		}

		var image = UIImage.LoadFromData(NSData.FromUrl(new NSUrl(MediaElement.MetadataArtworkUrl))) ?? new UIImage();
		var imageView = CreatePosterImageView(image);

		PlayerViewController.ContentOverlayView.AddSubview(imageView);
		SetupPosterConstraints(imageView, image);
	}

	UIImageView CreatePosterImageView(UIImage image)
	{
		return new UIImageView(image)
		{
			ContentMode = UIViewContentMode.ScaleAspectFit,
			TranslatesAutoresizingMaskIntoConstraints = false,
			ClipsToBounds = true,
			AutoresizingMask = UIViewAutoresizing.FlexibleDimensions
		};
	}

	void SetupPosterConstraints(UIImageView imageView, UIImage image)
	{
		if (PlayerViewController?.ContentOverlayView is null)
		{
			return;
		}

		NSLayoutConstraint.ActivateConstraints(
		[
			imageView.CenterXAnchor.ConstraintEqualTo(PlayerViewController.ContentOverlayView.CenterXAnchor),
			imageView.CenterYAnchor.ConstraintEqualTo(PlayerViewController.ContentOverlayView.CenterYAnchor),
			imageView.WidthAnchor.ConstraintLessThanOrEqualTo(PlayerViewController.ContentOverlayView.WidthAnchor),
			imageView.HeightAnchor.ConstraintLessThanOrEqualTo(PlayerViewController.ContentOverlayView.HeightAnchor),

			// Maintain the aspect ratio
			imageView.WidthAnchor.ConstraintEqualTo(imageView.HeightAnchor, image.Size.Width / image.Size.Height)
		]);
	}

	protected virtual partial void PlatformUpdateSpeed()
	{
		if (Player is null)
		{
			return;
		}

		// First time we're getting a playback speed and should NOT auto play, do nothing.
		if (!isInitialSpeedSet && !MediaElement.ShouldAutoPlay)
		{
			isInitialSpeedSet = true;
			return;
		}

		Player.Rate = (float)MediaElement.Speed;
	}

	protected virtual partial void PlatformUpdateShouldShowPlaybackControls()
	{
		if (PlayerViewController is null)
		{
			return;
		}

		PlayerViewController.ShowsPlaybackControls =
			MediaElement.ShouldShowPlaybackControls;
	}

	protected virtual partial void PlatformUpdatePosition()
	{
		if (Player is null)
		{
			return;
		}

		if (PlayerItem is not null)
		{
			if (PlayerItem.Duration == CMTime.Indefinite)
			{
				var range = PlayerItem.SeekableTimeRanges?.LastOrDefault();

				if (range?.CMTimeRangeValue is not null)
				{
					MediaElement.Duration = ConvertTime(range.CMTimeRangeValue.Duration);
					MediaElement.Position = ConvertTime(PlayerItem.CurrentTime);
				}
			}
			else
			{
				MediaElement.Duration = ConvertTime(PlayerItem.Duration);
				MediaElement.Position = ConvertTime(PlayerItem.CurrentTime);
			}
		}
		else
		{
			Player.Pause();
			MediaElement.Duration = MediaElement.Position = TimeSpan.Zero;
		}
	}

	protected virtual partial void PlatformUpdateVolume()
	{
		if (Player is null)
		{
			return;
		}

		var volumeDiff = Math.Abs(Player.Volume - MediaElement.Volume);
		if (volumeDiff > 0.01)
		{
			Player.Volume = (float)MediaElement.Volume;
		}
	}


	protected virtual partial void PlatformUpdateShouldKeepScreenOn()
	{
		if (Player is null)
		{
			return;
		}

		UIApplication.SharedApplication.IdleTimerDisabled = MediaElement.ShouldKeepScreenOn;
	}


	protected virtual partial void PlatformUpdateShouldMute()
	{
		if (Player is null)
		{
			return;
		}

		Player.Muted = MediaElement.ShouldMute;
	}

	protected virtual partial void PlatformUpdateShouldLoopPlayback()
	{
		// no-op we loop through using the PlayedToEndObserver
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="MediaManager"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (Player is not null)
			{
				PauseAndCleanupPlayer();
				CleanupObservers();
				DisposePlayer();
			}

			DisposeViewController();
		}
	}

	void PauseAndCleanupPlayer()
	{
		if (Player is null)
		{
			return;
		}

		Player.Pause();
		Player.InvokeOnMainThread(UIApplication.SharedApplication.EndReceivingRemoteControlEvents);
		// disable the idle timer so screen turns off when media is not playing
		UIApplication.SharedApplication.IdleTimerDisabled = false;
		var audioSession = AVAudioSession.SharedInstance();
		audioSession.SetActive(false);

		DestroyErrorObservers();
		DestroyPlayedToEndObserver();
	}

	void CleanupObservers()
	{
		RateObserver?.Dispose();
		RateObserver = null;

		CurrentItemErrorObserver?.Dispose();
		CurrentItemErrorObserver = null;

		MutedObserver?.Dispose();
		MutedObserver = null;

		VolumeObserver?.Dispose();
		VolumeObserver = null;

		StatusObserver?.Dispose();
		StatusObserver = null;

		TimeControlStatusObserver?.Dispose();
		TimeControlStatusObserver = null;
	}

	void DisposePlayer()
	{
		if (Player is null)
		{
			return;
		}

		Player.ReplaceCurrentItemWithPlayerItem(null);
		Player.Dispose();
		Player = null;
	}

	void DisposeViewController()
	{
		PlayerViewController?.Dispose();
		PlayerViewController = null;
	}

	static TimeSpan ConvertTime(CMTime cmTime)
	{
		return TimeSpan.FromSeconds(double.IsNaN(cmTime.Seconds) ? 0 : cmTime.Seconds);
	}

	static (int Width, int Height) GetVideoDimensions(AVPlayerItem avPlayerItem)
	{
		// Create an AVAsset instance with the video file URL
		var asset = avPlayerItem.Asset;

		// Retrieve the video track
		var videoTrack = asset.TracksWithMediaType(AVMediaTypes.Video.GetConstant() ?? "0").FirstOrDefault();

		if (videoTrack is not null)
		{
			// Get the natural size of the video
			var size = videoTrack.NaturalSize;
			var preferredTransform = videoTrack.PreferredTransform;

			// Apply the preferred transform to get the correct dimensions
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

		// If all else fails, just return 0, 0
		return (0, 0);
	}


	void AddStatusObservers()
	{
		if (Player is null)
		{
			return;
		}

		MutedObserver = Player.AddObserver("muted", ValueObserverOptions, MutedChanged);
		VolumeObserver = Player.AddObserver("volume", ValueObserverOptions, VolumeChanged);
		StatusObserver = Player.AddObserver("status", ValueObserverOptions, StatusChanged);
		TimeControlStatusObserver = Player.AddObserver("timeControlStatus", ValueObserverOptions, TimeControlStatusChanged);
		RateObserver = AVPlayer.Notifications.ObserveRateDidChange(RateChanged);
	}


	void VolumeChanged(NSObservedChange e)
	{
		if (Player is null)
		{
			return;
		}

		var volumeDiff = Math.Abs(Player.Volume - MediaElement.Volume);
		if (volumeDiff > 0.01)
		{
			MediaElement.Volume = Player.Volume;
		}
	}


	void MutedChanged(NSObservedChange e)
	{
		if (Player is null)
		{
			return;
		}

		MediaElement.ShouldMute = Player.Muted;
	}

	void AddErrorObservers()
	{
		DestroyErrorObservers();

		ItemFailedToPlayToEndTimeObserver = AVPlayerItem.Notifications.ObserveItemFailedToPlayToEndTime(ErrorOccurred);
		PlaybackStalledObserver = AVPlayerItem.Notifications.ObservePlaybackStalled(ErrorOccurred);
		ErrorObserver = AVPlayerItem.Notifications.ObserveNewErrorLogEntry(ErrorOccurred);
	}

	void AddPlayedToEndObserver()
	{
		DestroyPlayedToEndObserver();

		PlayedToEndObserver = AVPlayerItem.Notifications.ObserveDidPlayToEndTime(PlayedToEnd);
	}

	void DestroyErrorObservers()
	{
		ItemFailedToPlayToEndTimeObserver?.Dispose();
		PlaybackStalledObserver?.Dispose();
		ErrorObserver?.Dispose();
	}

	void DestroyPlayedToEndObserver()
	{
		PlayedToEndObserver?.Dispose();
	}


	void StatusChanged(NSObservedChange obj)
	{
		if (Player is null)
		{
			return;
		}

		var newState = Player.Status switch
		{
			AVPlayerStatus.Unknown => MediaElementState.Stopped,
			AVPlayerStatus.ReadyToPlay => MediaElementState.Paused,
			AVPlayerStatus.Failed => MediaElementState.Failed,
			_ => MediaElement.CurrentState
		};

		MediaElement.CurrentStateChanged(newState);
	}


	void TimeControlStatusChanged(NSObservedChange obj)
	{
		if (Player is null || Player.Status is AVPlayerStatus.Unknown
						   || Player.CurrentItem?.Error is not null)
		{
			return;
		}

		var newState = Player.TimeControlStatus switch
		{
			AVPlayerTimeControlStatus.Paused => MediaElementState.Paused,
			AVPlayerTimeControlStatus.Playing => MediaElementState.Playing,
			AVPlayerTimeControlStatus.WaitingToPlayAtSpecifiedRate => MediaElementState.Buffering,
			_ => MediaElement.CurrentState
		};

		metaData?.SetMetadata(PlayerItem, MediaElement);

		MediaElement.CurrentStateChanged(newState);
	}


	void ErrorOccurred(object? sender, NSNotificationEventArgs args)
	{
		string message;

		var error = Player?.CurrentItem?.Error;
		if (error is not null)
		{
			message = error.LocalizedDescription;

			MediaElement.MediaFailed(new MediaFailedEventArgs(message));
			Logger.LogError("{LogMessage}", message);
		}
		else
		{
			// Non-fatal error, just log
			message = args.Notification?.ToString() ??
					  "Media playback failed for an unknown reason.";

			Logger?.LogWarning("{LogMessage}", message);
		}
	}


	void PlayedToEnd(object? sender, NSNotificationEventArgs args)
	{
		if (Player is null || args.Notification.Object != Player.CurrentItem)
		{
			return;
		}

		if (MediaElement.ShouldLoopPlayback)
		{
			Player.Seek(CMTime.Zero);
			Player.Play();
		}
		else
		{
			try
			{
				DispatchQueue.MainQueue.DispatchAsync(MediaElement.MediaEnded);
			}
			catch (Exception e)
			{
				Logger.LogWarning(e, "{LogMessage}", "Failed to play media to end.");
			}
		}
	}


	void RateChanged(object? sender, NSNotificationEventArgs args)
	{
		if (Player is null)
		{
			return;
		}

		if (!AreFloatingPointNumbersEqual(MediaElement.Speed, Player.Rate))
		{
			MediaElement.Speed = Player.Rate;
			if (metaData is not null)
			{
				metaData.NowPlayingInfo.PlaybackRate = (float)MediaElement.Speed;
				MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = metaData.NowPlayingInfo;
			}
		}
	}
}