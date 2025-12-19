#nullable enable

using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;
#if ANDROID
using Android.App;
using Android.Content;
#endif

namespace CommunityToolkit.Maui.Core.Handlers;

public partial class MediaElementHandler : ViewHandler<MediaElement, MauiMediaElement>, IDisposable
{
	/// <summary>
	/// Maps the <see cref="IMediaElement.ShouldLoopPlayback"/> property between the abstract
	/// <see cref="MediaElement"/> and platform counterpart.
	/// </summary>
	/// <param name="handler">The associated handler.</param>
	/// <param name="mediaElement">The associated <see cref="MediaElement"/> instance.</param>
	public static void ShouldLoopPlayback(MediaElementHandler handler, MediaElement mediaElement)
	{
		handler.MediaManager?.UpdateShouldLoopPlayback();
	}


	protected override MauiMediaElement CreatePlatformView()
	{
		// Use the handler's MauiContext - guaranteed to be available when PrepareAndPlayAsync is called
		// (bootstrap completes before PrepareAndPlayAsync is called)
		if (MauiContext == null)
		{
			throw new InvalidOperationException("MauiContext is null - ensure bootstrap has completed before calling PrepareAndPlayAsync");
		}

		var dispatcher = GetDispatcher();
		MediaManager ??= new MediaManager(MauiContext, VirtualView, dispatcher);

		// Always use None (headless mode) for audio-only playback
		// This app plays Bible readings and music (audio-only), so no UI view is needed
		// ExoPlayer works perfectly in headless mode for audio playback
		var (_, playerView) = MediaManager.CreatePlatformView(AndroidViewType.None);

		// Return lightweight view for headless, or wrap PlayerView if UI exists
		if (playerView == null)
		{
			// Headless mode - create lightweight MauiMediaElement without PlayerView
			if (Context == null)
			{
				throw new InvalidOperationException("Context is null - cannot create platform view");
			}
			return new MauiMediaElement(Context);
		}

		// UI mode - we have a real context and PlayerView
		if (Context == null)
		{
			throw new InvalidOperationException("Context is null but PlayerView was created");
		}

		return new MauiMediaElement(Context, playerView);
	}

	IDispatcher GetDispatcher()
	{
		// Get dispatcher - try current thread first, then Application.Current dispatcher
		// After bootstrap completes, Application.Current.Dispatcher should always be available
		var dispatcher = Dispatcher.GetForCurrentThread();
		if (dispatcher == null)
		{
			// Try to get dispatcher from Application.Current (works in background services after bootstrap)
			dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
		}

		if (dispatcher == null)
		{
			throw new InvalidOperationException("Dispatcher cannot be null - ensure bootstrap has completed before calling CreatePlatformView");
		}

		return dispatcher;
	}

	protected override void DisconnectHandler(MauiMediaElement platformView)
	{
		// Handle headless mode where platformView may be null
		if (platformView != null)
		{
			base.DisconnectHandler(platformView);
			platformView.Dispose();
		}

		// Dispose() will handle MediaManager disposal (releases MediaSession and stops MediaControlsService)
		// This works for both UI mode and headless mode
		Dispose();
	}
}