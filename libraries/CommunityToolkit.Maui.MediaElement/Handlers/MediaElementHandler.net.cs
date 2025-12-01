using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;

namespace CommunityToolkit.Maui.Core.Handlers;

public partial class MediaElementHandler : ViewHandler<MediaElement, PlatformMediaElement>
{
	/// <inheritdoc/>
	protected override PlatformMediaElement CreatePlatformView() => throw new NotImplementedException();

	// Stub implementations for net10.0 base target - real implementations are in platform-specific files
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public static void MapAspect(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapPosition(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapShouldKeepScreenOn(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapShouldMute(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapShouldShowPlaybackControls(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapSource(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapSpeed(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapStatusUpdated(MediaElementHandler handler, MediaElement mediaElement, object? args) => throw new NotImplementedException();
	public static void MapVolume(MediaElementHandler handler, MediaElement mediaElement) => throw new NotImplementedException();
	public static void MapPlayRequested(MediaElementHandler handler, MediaElement mediaElement, object? args) => throw new NotImplementedException();
	public static void MapPauseRequested(MediaElementHandler handler, MediaElement mediaElement, object? args) => throw new NotImplementedException();
	public static void MapSeekRequested(MediaElementHandler handler, MediaElement mediaElement, object? args) => throw new NotImplementedException();
	public static void MapStopRequested(MediaElementHandler handler, MediaElement mediaElement, object? args) => throw new NotImplementedException();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}