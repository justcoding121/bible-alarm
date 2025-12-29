using System.Diagnostics;
using System.Numerics;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System.Display;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
using ParentWindow = CommunityToolkit.Maui.Extensions.PageExtensions.ParentWindow;
using Stretch = Microsoft.UI.Xaml.Media.Stretch;
using WindowsMediaElement = Windows.Media.Playback.MediaPlayer;
using WinMediaSource = Windows.Media.Core.MediaSource;

namespace CommunityToolkit.Maui.Core.Views;

partial class MediaManager : IDisposable
{
	Metadata? metadata;
	SystemMediaTransportControls? systemMediaControls;
	WindowsMediaElement? headlessMediaPlayer; // For headless mode (audio-only)

	/// <summary>
	/// Gets the underlying MediaPlayer, whether from Player (UI mode) or headlessMediaPlayer (headless mode).
	/// </summary>
	WindowsMediaElement? GetMediaPlayer() => Player?.MediaPlayer ?? headlessMediaPlayer;

	// States that allow changing position
	readonly IReadOnlyList<MediaElementState> allowUpdatePositionStates =
	[
		MediaElementState.Playing,
		MediaElementState.Paused,
		MediaElementState.Stopped,
	];

	// The requests to keep display active are cumulative, this bool makes sure it only gets requested once
	bool displayActiveRequested;

	/// <summary>
	/// The <see cref="DisplayRequest"/> is used to enable the <see cref="Maui.Views.MediaElement.ShouldKeepScreenOn"/> functionality.
	/// </summary>
	/// <remarks>
	/// Calls to <see cref="Windows.System.Display.DisplayRequest.RequestActive"/> and <see cref="DisplayRequest.RequestRelease"/> should be in balance.
	/// Not doing so will result in the screen staying on and negatively impacting the environment :(
	/// </remarks>
	protected DisplayRequest DisplayRequest { get; } = new();

	/// <summary>
	/// Creates the corresponding platform view of <see cref="MediaElement"/> on Windows.
	/// </summary>
	/// <returns>The platform native counterpart of <see cref="MediaElement"/>. Returns null in headless mode.</returns>
	public PlatformMediaElement? CreatePlatformView()
	{
		// In headless mode (audio-only), we don't need a MediaPlayerElement
		// Windows MediaPlayer can work without a UI element for audio playback
		// Create the MediaPlayer directly without wrapping it in a MediaPlayerElement
		WindowsMediaElement mediaElement = new();
		mediaElement.MediaOpened += OnMediaElementMediaOpened;
		mediaElement.MediaFailed += OnMediaElementMediaFailed;
		mediaElement.MediaEnded += OnMediaElementMediaEnded;
		mediaElement.VolumeChanged += OnMediaElementVolumeChanged;
		mediaElement.IsMutedChanged += OnMediaElementIsMutedChanged;

		// Store the MediaPlayer directly (not wrapped in MediaPlayerElement)
		// This allows headless operation without requiring a UI element
		Player = null; // No MediaPlayerElement in headless mode
		headlessMediaPlayer = mediaElement; // Store the MediaPlayer directly for headless mode

		// Set up system media transport controls for headless mode
		mediaElement.SystemMediaTransportControls.IsEnabled = false;
		systemMediaControls = mediaElement.SystemMediaTransportControls;

		// Set up event handlers for headless mode
		mediaElement.PlaybackSession.NaturalVideoSizeChanged += OnNaturalVideoSizeChanged;
		mediaElement.PlaybackSession.PlaybackRateChanged += OnPlaybackSessionPlaybackRateChanged;
		mediaElement.PlaybackSession.PlaybackStateChanged += OnPlaybackSessionPlaybackStateChanged;
		mediaElement.PlaybackSession.SeekCompleted += OnPlaybackSessionSeekCompleted;

		// Return null to indicate headless mode
		return null;
	}

	/// <summary>
	/// Releases the managed and unmanaged resources used by the <see cref="MediaManager"/>.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual partial void PlatformPlay()
	{
		if (Player is not null)
		{
			Player.MediaPlayer.Play();
		}
		else if (headlessMediaPlayer is not null)
		{
			headlessMediaPlayer.Play();
		}

		if (MediaElement.ShouldKeepScreenOn
			&& !displayActiveRequested)
		{
			DisplayRequest.RequestActive();
			displayActiveRequested = true;
		}
	}

	protected virtual partial void PlatformPause()
	{
		if (Player is not null)
		{
			Player.MediaPlayer.Pause();
		}
		else if (headlessMediaPlayer is not null)
		{
			headlessMediaPlayer.Pause();
		}

		if (displayActiveRequested)
		{
			DisplayRequest.RequestRelease();
			displayActiveRequested = false;
		}
	}

	protected virtual async partial Task PlatformSeek(TimeSpan position, CancellationToken token)
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer?.CanSeek is true)
		{
			if (Dispatcher.IsDispatchRequired)
			{
				await Dispatcher.DispatchAsync(() => mediaPlayer.Position = position).WaitAsync(token);
			}
			else
			{
				token.ThrowIfCancellationRequested();
				mediaPlayer.Position = position;
			}
		}
	}

	protected virtual partial void PlatformStop()
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}

		// There's no Stop method so pause the video and reset its position
		mediaPlayer.Pause();
		mediaPlayer.Position = TimeSpan.Zero;

		MediaElement.CurrentStateChanged(MediaElementState.Stopped);

		if (displayActiveRequested)
		{
			DisplayRequest.RequestRelease();
			displayActiveRequested = false;
		}
	}

	protected virtual partial void PlatformUpdateAspect()
	{
		if (Player is null)
		{
			return;
		}

		Player.Stretch = MediaElement.Aspect switch
		{
			Aspect.Fill => Stretch.Fill,
			Aspect.AspectFill => Stretch.UniformToFill,
			_ => Stretch.Uniform,
		};
	}

	protected virtual partial void PlatformUpdateSpeed()
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}

		var previousSpeed = mediaPlayer.PlaybackRate;
		mediaPlayer.PlaybackRate = MediaElement.Speed;

		// Only trigger once when going to the paused state
		if (IsZero(MediaElement.Speed) && previousSpeed > 0)
		{
			mediaPlayer.Pause();
		}
		// Only trigger once when we move from the paused state
		else if (MediaElement.Speed > 0 && IsZero(previousSpeed))
		{
			MediaElement.Play();
		}
	}

	protected virtual partial void PlatformUpdateShouldShowPlaybackControls()
	{
		if (Player is null)
		{
			return;
		}

		Player.AreTransportControlsEnabled =
			MediaElement.ShouldShowPlaybackControls;
	}

	protected virtual partial void PlatformUpdatePosition()
	{
		if (Application.Current?.Windows is null || Application.Current.Windows.Count == 0)
		{
			return;
		}
		if (!ParentWindow.Exists)
		{
			// Parent window is null, so we can't update the position
			// This is a workaround for a bug where the timer keeps running after the window is closed
			return;
		}

		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is not null
			&& allowUpdatePositionStates.Contains(MediaElement.CurrentState))
		{
			MediaElement.Position = mediaPlayer.Position;
		}
	}

	protected virtual partial void PlatformUpdateVolume()
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}

		// If currently muted, ignore
		if (MediaElement.ShouldMute)
		{
			return;
		}

		if (Dispatcher.IsDispatchRequired)
		{
			Dispatcher.Dispatch(() => mediaPlayer.Volume = MediaElement.Volume);
		}
		else
		{
			mediaPlayer.Volume = MediaElement.Volume;
		}
	}

	protected virtual partial void PlatformUpdateShouldKeepScreenOn()
	{
		if (MediaElement.ShouldKeepScreenOn)
		{
			if (allowUpdatePositionStates.Contains(MediaElement.CurrentState)
				&& !displayActiveRequested)
			{
				DisplayRequest.RequestActive();
				displayActiveRequested = true;
			}
		}
		else
		{
			if (displayActiveRequested)
			{
				DisplayRequest.RequestRelease();
				displayActiveRequested = false;
			}
		}
	}

	protected virtual partial void PlatformUpdateShouldMute()
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}
		Dispatcher.Dispatch(() => mediaPlayer.IsMuted = MediaElement.ShouldMute);
	}

	protected virtual async partial ValueTask PlatformUpdateSource()
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}

		// Update poster source only if we have a Player (UI mode)
		if (Player is not null)
		{
			await Dispatcher.DispatchAsync(() => Player.PosterSource = new BitmapImage());
		}

		if (MediaElement.Source is null)
		{
			// In headless mode, we set source directly on MediaPlayer
			// In UI mode, we set source on MediaPlayerElement
			if (Player is not null)
			{
				Player.Source = null;
			}
			else
			{
				mediaPlayer.Source = null;
			}
			MediaElement.MediaWidth = MediaElement.MediaHeight = 0;

			MediaElement.CurrentStateChanged(MediaElementState.None);

			return;
		}

		MediaElement.Position = TimeSpan.Zero;
		MediaElement.Duration = TimeSpan.Zero;

		if (MediaElement.Source is UriMediaSource uriMediaSource)
		{
			var uri = uriMediaSource.Uri?.AbsoluteUri;
			if (!string.IsNullOrWhiteSpace(uri))
			{
				var source = WinMediaSource.CreateFromUri(new Uri(uri));
				if (Player is not null)
				{
					Player.AutoPlay = MediaElement.ShouldAutoPlay;
					Player.Source = source;
				}
				else
				{
					mediaPlayer.Source = source;
					if (MediaElement.ShouldAutoPlay)
					{
						mediaPlayer.Play();
					}
				}
			}
		}
		else if (MediaElement.Source is FileMediaSource fileMediaSource)
		{
			var filename = fileMediaSource.Path;
			if (!string.IsNullOrWhiteSpace(filename))
			{
				StorageFile storageFile = await StorageFile.GetFileFromPathAsync(filename);
				var source = WinMediaSource.CreateFromStorageFile(storageFile);
				if (Player is not null)
				{
					Player.AutoPlay = MediaElement.ShouldAutoPlay;
					Player.Source = source;
				}
				else
				{
					mediaPlayer.Source = source;
					if (MediaElement.ShouldAutoPlay)
					{
						mediaPlayer.Play();
					}
				}
			}
		}
		else if (MediaElement.Source is ResourceMediaSource resourceMediaSource)
		{
			if (string.IsNullOrWhiteSpace(resourceMediaSource.Path))
			{
				Logger.LogInformation("ResourceMediaSource Path is null or empty");
				return;
			}

			string path = GetFullAppPackageFilePath(resourceMediaSource.Path);
			if (!string.IsNullOrWhiteSpace(path))
			{
				var source = WinMediaSource.CreateFromUri(new Uri(path));
				if (Player is not null)
				{
					Player.AutoPlay = MediaElement.ShouldAutoPlay;
					Player.Source = source;
				}
				else
				{
					mediaPlayer.Source = source;
					if (MediaElement.ShouldAutoPlay)
					{
						mediaPlayer.Play();
					}
				}
			}
		}
	}

	protected virtual partial void PlatformUpdateShouldLoopPlayback()
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}

		mediaPlayer.IsLoopingEnabled = MediaElement.ShouldLoopPlayback;
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="MediaManager"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			var mediaPlayer = GetMediaPlayer();
			if (mediaPlayer is not null)
			{
				if (displayActiveRequested)
				{
					DisplayRequest.RequestRelease();
					displayActiveRequested = false;
				}

				mediaPlayer.MediaOpened -= OnMediaElementMediaOpened;
				mediaPlayer.MediaFailed -= OnMediaElementMediaFailed;
				mediaPlayer.MediaEnded -= OnMediaElementMediaEnded;
				mediaPlayer.VolumeChanged -= OnMediaElementVolumeChanged;
				mediaPlayer.IsMutedChanged -= OnMediaElementIsMutedChanged;

				if (mediaPlayer.PlaybackSession is not null)
				{
					mediaPlayer.PlaybackSession.NaturalVideoSizeChanged -= OnNaturalVideoSizeChanged;
					mediaPlayer.PlaybackSession.PlaybackRateChanged -= OnPlaybackSessionPlaybackRateChanged;
					mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackSessionPlaybackStateChanged;
					mediaPlayer.PlaybackSession.SeekCompleted -= OnPlaybackSessionSeekCompleted;
				}

				// Dispose headless MediaPlayer if in headless mode
				if (headlessMediaPlayer is not null)
				{
					mediaPlayer.Pause();
					mediaPlayer.Source = null;
					mediaPlayer.Dispose();
					headlessMediaPlayer = null;
				}
			}
		}
	}

	static string GetFullAppPackageFilePath(in string filename)
	{
		ArgumentNullException.ThrowIfNull(filename);

		var normalizedFilename = NormalizePath(filename);
		return Path.Combine(AppPackageService.FullAppPackageFilePath, normalizedFilename);

		static string NormalizePath(string filename) => filename.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
	}

	static bool IsZero<TValue>(TValue numericValue) where TValue : INumber<TValue>
	{
		return TValue.IsZero(numericValue);
	}

	async ValueTask UpdateMetadata()
	{
		if (systemMediaControls is null)
		{
			return;
		}

		metadata ??= new(systemMediaControls, MediaElement, Dispatcher);
		metadata.SetMetadata(MediaElement);
		if (string.IsNullOrEmpty(MediaElement.MetadataArtworkUrl))
		{
			return;
		}
		if (!Uri.TryCreate(MediaElement.MetadataArtworkUrl, UriKind.RelativeOrAbsolute, out var metadataArtworkUri))
		{
			Logger.LogError("{MediaElement} unable to update artwork because {MetadataArtworkUrl} is not a valid URI", nameof(MediaElement), nameof(MediaElement.MetadataArtworkUrl));
			return;
		}

		// Update poster source only if we have a Player (UI mode)
		if (Player is not null)
		{
			if (Dispatcher.IsDispatchRequired)
			{
				await Dispatcher.DispatchAsync(() => UpdatePosterSource(Player, metadataArtworkUri));
			}
			else
			{
				UpdatePosterSource(Player, metadataArtworkUri);
			}
		}

		static void UpdatePosterSource(in PlatformMediaElement player, in Uri metadataArtworkUri)
		{
			player.PosterSource = new BitmapImage(metadataArtworkUri);
		}
	}

	async void OnMediaElementMediaOpened(WindowsMediaElement sender, object args)
	{
		var mediaPlayer = GetMediaPlayer();
		if (mediaPlayer is null)
		{
			return;
		}

		if (Dispatcher.IsDispatchRequired)
		{
			Dispatcher.Dispatch(() => SetDuration(MediaElement, mediaPlayer));
		}
		else
		{
			SetDuration(MediaElement, mediaPlayer);
		}

		MediaElement.MediaOpened();

		await UpdateMetadata();

		static void SetDuration(in IMediaElement mediaElement, in WindowsMediaElement mediaPlayer)
		{
			mediaElement.Duration = mediaPlayer.NaturalDuration == TimeSpan.MaxValue
				? TimeSpan.Zero
				: mediaPlayer.NaturalDuration;
		}
	}

	void OnMediaElementMediaEnded(WindowsMediaElement sender, object args)
	{
		MediaElement?.MediaEnded();
	}

	void OnMediaElementMediaFailed(WindowsMediaElement sender, MediaPlayerFailedEventArgs args)
	{
		string errorMessage = string.Empty;
		string errorCode = string.Empty;
		string error = args.Error.ToString();

		if (!string.IsNullOrWhiteSpace(args.ErrorMessage))
		{
			errorMessage = $"Error message: {args.ErrorMessage}";
		}

		if (args.ExtendedErrorCode != null)
		{
			errorCode = $"Error code: {args.ExtendedErrorCode.Message}";
		}

		var message = string.Join(", ",
			new[] { error, errorCode, errorMessage }
			.Where(s => !string.IsNullOrEmpty(s)));

		MediaElement?.MediaFailed(new MediaFailedEventArgs(message));

		Logger?.LogError("{LogMessage}", message);
	}

	void OnMediaElementIsMutedChanged(WindowsMediaElement sender, object args)
	{
		MediaElement.ShouldMute = sender.IsMuted;
	}

	void OnMediaElementVolumeChanged(WindowsMediaElement sender, object args)
	{
		MediaElement.Volume = sender.Volume;
	}

	void OnNaturalVideoSizeChanged(MediaPlaybackSession sender, object args)
	{
		if (MediaElement is not null)
		{
			MediaElement.MediaWidth = (int)sender.NaturalVideoWidth;
			MediaElement.MediaHeight = (int)sender.NaturalVideoHeight;
		}
	}

	void OnPlaybackSessionPlaybackRateChanged(MediaPlaybackSession sender, object args)
	{
		if (AreFloatingPointNumbersEqual(MediaElement.Speed, sender.PlaybackRate))
		{
			if (Dispatcher.IsDispatchRequired)
			{
				Dispatcher.Dispatch(() => UpdateSpeed(MediaElement, sender.PlaybackRate));
			}
			else
			{
				UpdateSpeed(MediaElement, sender.PlaybackRate);
			}
		}

		static void UpdateSpeed(in IMediaElement mediaElement, in double playbackRate) => mediaElement.Speed = playbackRate;
	}

	void OnPlaybackSessionPlaybackStateChanged(MediaPlaybackSession sender, object args)
	{
		var newState = sender.PlaybackState switch
		{
			MediaPlaybackState.Buffering => MediaElementState.Buffering,
			MediaPlaybackState.Playing => MediaElementState.Playing,
			MediaPlaybackState.Paused => MediaElementState.Paused,
			MediaPlaybackState.Opening => MediaElementState.Opening,
			_ => MediaElementState.None,
		};

		MediaElement?.CurrentStateChanged(newState);
		if (sender.PlaybackState == MediaPlaybackState.Playing && IsZero(sender.PlaybackRate))
		{
			Dispatcher.Dispatch(() =>
			{
				sender.PlaybackRate = 1;
			});
		}
	}

	void OnPlaybackSessionSeekCompleted(MediaPlaybackSession sender, object args)
	{
		MediaElement?.SeekCompleted();
	}
}