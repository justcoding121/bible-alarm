#nullable enable

using System;
using System.Threading;
using Android.App;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Coordinates foreground service ownership between MediaElement playback and Android Auto.
/// Android only allows one foreground service at a time, so this ensures proper handoff.
/// Only starts Android Auto foreground service when Android Auto is actually connected.
/// </summary>
public sealed class ForegroundServiceCoordinator
{
    private static readonly ILogger logger = Log.ForContext<ForegroundServiceCoordinator>();
    private static readonly Lock @lock = new();
    private static readonly ForegroundServiceStateManager state = new();
    
    /// <summary>
    /// Represents the current owner of the foreground service.
    /// </summary>
    public enum ForegroundServiceOwner
    {
        None,
        MediaElement,      // MediaControlsService from MediaElement library (uses notification ID 1)
        AndroidAuto        // Android Auto connection service (uses notification ID 2)
    }

    /// <summary>
    /// Called when Android Auto connects (OnGetRoot or OnBind for LegacyMediaBrowserService, 
    /// or OnCreateSession for CarAppService).
    /// Only starts foreground service if MediaElement is not active.
    /// </summary>
    /// <param name="service">The Android Auto service instance</param>
    public static void OnAndroidAutoConnected(Service service)
    {
        lock (@lock)
        {
            if (state.IsAndroidAutoConnected)
            {
                logger.Debug("Android Auto already marked as connected");
                state.SetAndroidAutoConnected(true, service);
                return;
            }

            state.SetAndroidAutoConnected(true, service);
            logger.Information("Android Auto connected - will start foreground service if metadata is available and MediaElement is not active");

            if (ForegroundServiceValidator.IsMediaElementActive(state))
            {
                logger.Information("Android Auto connected but MediaElement is active - will start foreground service when MediaElement stops");
                return;
            }

            TryStartForegroundWithMetadata(service);
        }
    }

    private static void TryStartForegroundWithMetadata(Service service)
    {
        var session = MediaSessionHelper.Create();
        if (session?.Controller?.Metadata == null)
        {
            logger.Debug("Android Auto connected but MediaSession not ready - will start foreground when metadata is set");
            return;
        }

        var hasMetadata = !string.IsNullOrEmpty(
            session.Controller.Metadata.GetString(MediaMetadataCompat.MetadataKeyTitle));
        
        if (hasMetadata)
        {
            logger.Information("Metadata available - starting Android Auto foreground service immediately");
            RequestForAndroidAuto(service, session);
        }
        else
        {
            logger.Debug("Android Auto connected but no metadata yet - will start foreground when metadata is set");
        }
    }

    /// <summary>
    /// Called when Android Auto disconnects (OnUnbind for LegacyMediaBrowserService).
    /// Only stops the foreground service if Android Auto is the current owner.
    /// MediaElement manages its own foreground service lifecycle, so we don't interfere with it.
    /// </summary>
    public static void OnAndroidAutoDisconnected()
    {
        lock (@lock)
        {
            if (!state.IsAndroidAutoConnected)
            {
                logger.Debug("Android Auto already marked as disconnected");
                return;
            }

            state.SetAndroidAutoConnected(false);
            logger.Information("Android Auto disconnected");

            if (state.CurrentOwner == ForegroundServiceOwner.AndroidAuto)
            {
                logger.Information("Stopping Android Auto foreground service and clearing service reference");
                ForegroundServiceOperations.StopForeground(state.AndroidAutoService);
                state.SetOwner(ForegroundServiceOwner.None);
            }
            else
            {
                logger.Debug("Android Auto disconnected but was not the foreground service owner (current owner: {CurrentOwner}) - not stopping foreground service", 
                    state.CurrentOwner);
            }
            
            state.ClearAndroidAutoService();
        }
    }

    /// <summary>
    /// Called when MediaElement starts playing (detected via playback state changes).
    /// If Android Auto is currently foreground, it will be stopped first to ensure smooth transition.
    /// </summary>
    public static void OnPlaybackStarted()
    {
        lock (@lock)
        {
            state.SetMediaElementPlaying(true);
            
            if (state.CurrentOwner == ForegroundServiceOwner.MediaElement)
            {
                logger.Debug("MediaElement already owns foreground service");
                return;
            }

            logger.Information("Playback started - MediaElement requesting foreground service ownership (current owner: {CurrentOwner})", 
                state.CurrentOwner);

            if (state.CurrentOwner == ForegroundServiceOwner.AndroidAuto)
            {
                logger.Information("Stopping Android Auto foreground service before MediaElement starts");
                ForegroundServiceOperations.StopForeground(state.AndroidAutoService);
                // Small delay to ensure Android Auto foreground service is fully stopped
                Thread.Sleep(100);
                logger.Information("Android Auto foreground stopped - MediaElement can now start");
            }

            // MediaElement's MediaControlsService will start itself via MediaManager.StartService()
            state.SetOwner(ForegroundServiceOwner.MediaElement);
            logger.Information("Foreground service ownership transferred to MediaElement");
        }
    }

    /// <summary>
    /// Called when MediaElement stops playing (detected via playback state changes).
    /// Releases ownership but doesn't immediately start Android Auto foreground service.
    /// Android Auto foreground service should be started after MediaElement is fully disposed
    /// and metadata is set (via HandleSetDefaultScheduleMetadata).
    /// </summary>
    public static void OnPlaybackStopped()
    {
        lock (@lock)
        {
            state.SetMediaElementPlaying(false);
            
            if (state.CurrentOwner != ForegroundServiceOwner.MediaElement)
            {
                logger.Debug("MediaElement does not own foreground service (current owner: {CurrentOwner})", state.CurrentOwner);
                return;
            }

            logger.Information("Playback stopped - MediaElement releasing foreground service ownership (will wait for disposal before starting Android Auto foreground)");
            state.SetOwner(ForegroundServiceOwner.None);
        }
    }

    /// <summary>
    /// Called when MediaElement is fully disposed and its notification/foreground service is removed.
    /// This ensures proper synchronization - MediaElement's notification is removed before
    /// Android Auto foreground service starts.
    /// </summary>
    public static void OnMediaElementDisposed()
    {
        lock (@lock)
        {
            if (state.CurrentOwner == ForegroundServiceOwner.MediaElement)
            {
                logger.Information("MediaElement disposed - releasing foreground service ownership");
                state.SetOwner(ForegroundServiceOwner.None);
            }
            
            logger.Debug("MediaElement disposal complete - Android Auto can now take ownership when metadata is set");
        }
    }

    /// <summary>
    /// Requests foreground service ownership for Android Auto.
    /// Only succeeds if Android Auto is connected and MediaElement is not currently playing.
    /// Should be called whenever default schedule metadata is set.
    /// Uses the stored service instance from when Android Auto connected.
    /// </summary>
    /// <param name="mediaSession">The MediaSessionCompat to attach to notification</param>
    /// <returns>True if ownership was granted or already owned, false if Android Auto not connected or MediaElement is active</returns>
    public static bool RequestForAndroidAuto(MediaSessionCompat mediaSession)
    {
        lock (@lock)
        {
            if (state.AndroidAutoService == null)
            {
                logger.Debug("Cannot start Android Auto foreground service - no service instance available (Android Auto may not be connected yet)");
                return false;
            }
            
            return RequestForAndroidAuto(state.AndroidAutoService, mediaSession);
        }
    }

    /// <summary>
    /// Requests foreground service ownership for Android Auto.
    /// Only succeeds if Android Auto is connected and MediaElement is not currently playing.
    /// Should be called whenever default schedule metadata is set.
    /// </summary>
    /// <param name="service">The Android Auto service instance (LegacyMediaBrowserService)</param>
    /// <param name="mediaSession">The MediaSessionCompat to attach to notification</param>
    /// <returns>True if ownership was granted or already owned, false if Android Auto not connected or MediaElement is active</returns>
    public static bool RequestForAndroidAuto(Service service, MediaSessionCompat mediaSession)
    {
        lock (@lock)
        {
            if (!ForegroundServiceValidator.CanStartAndroidAutoForeground(state))
            {
                return false;
            }

            if (ForegroundServiceValidator.ShouldUpdateAndroidAutoForeground(state))
            {
                logger.Debug("Android Auto already owns foreground service - updating notification");
                ForegroundServiceOperations.UpdateForeground(service, mediaSession);
                return true;
            }

            logger.Information("Android Auto requesting foreground service ownership (Android Auto is connected)");
            ForegroundServiceOperations.StartForeground(service, mediaSession);
            state.SetOwner(ForegroundServiceOwner.AndroidAuto);
            state.SetAndroidAutoConnected(true, service);
            logger.Information("Foreground service ownership granted to Android Auto");
            return true;
        }
    }

    /// <summary>
    /// Gets the current foreground service owner.
    /// </summary>
    public static ForegroundServiceOwner CurrentOwner => state.CurrentOwner;

    /// <summary>
    /// Gets whether Android Auto is currently connected.
    /// </summary>
    public static bool IsAndroidAutoConnected => state.IsAndroidAutoConnected;

}
