#nullable enable

using Android.App;
using Android.Content;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Coordinates foreground service ownership between MediaElement playback and Android Auto.
/// Android only allows one foreground service at a time, so this ensures proper handoff.
/// Only starts Android Auto foreground service when Android Auto is actually connected.
/// 
/// THREAD SAFETY: All public methods use locks to ensure thread-safe access to shared state.
/// The lock is reentrant, so nested calls from within the same lock are safe.
/// </summary>
public sealed class ForegroundServiceCoordinator
{
    private static readonly ILogger logger = Log.ForContext<ForegroundServiceCoordinator>();
    private static readonly Lock @lock = new(); // Reentrant lock - safe for nested calls
    private static readonly ForegroundServiceStateManager state = new();

    /// <summary>
    /// Represents the current owner of the foreground service.
    /// </summary>
    public enum ForegroundServiceOwner
    {
        None,
        MediaElement,      // MediaControlsService from MediaElement library (uses notification ID 1)
        AndroidAuto,       // Android Auto connection service (uses notification ID 2)
        Alarm              // Alarm foreground service (uses notification ID 2)
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
            logger.Information("Android Auto connected - will start foreground service if metadata is available and app is not in foreground");

            // Skip foreground service if app is already in foreground or has active foreground service
            if (ForegroundServiceValidator.ShouldSkipForegroundServiceStart(state))
            {
                logger.Information("Android Auto connected but app is in foreground or has active foreground service - skipping foreground service start");
                return;
            }

            TryStartForegroundWithMetadata(service);
        }
    }

    private static void TryStartForegroundWithMetadata(Service service)
    {
        var session = MediaSessionHelper.Create();
        if (session == null)
        {
            logger.Warning("Android Auto connected but MediaSession could not be created - cannot start foreground service");
            return;
        }

        // MediaSession is created before bootstrap, so it may not have metadata yet.
        // ForegroundNotificationHelper.CreateNotification handles missing metadata with fallbacks
        // ("Bible Alarm" / "Ready to play"), so we can start the foreground service immediately.
        // The notification will be updated when metadata is set later.
        var hasMetadata = session.Controller?.Metadata != null &&
            !string.IsNullOrEmpty(session.Controller.Metadata.GetString(MediaMetadataCompat.MetadataKeyTitle));

        if (hasMetadata)
        {
            logger.Information("Metadata available - starting Android Auto foreground service immediately");
        }
        else
        {
            logger.Information("Android Auto connected but no metadata yet (early bootstrap) - starting foreground service with fallback notification");
        }

        RequestForAndroidAuto(service, session);
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
    public static async Task OnPlaybackStarted()
    {
        bool needsDelay = false;

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

            if (state.CurrentOwner == ForegroundServiceOwner.AndroidAuto || state.CurrentOwner == ForegroundServiceOwner.Alarm)
            {
                var ownerType = state.CurrentOwner == ForegroundServiceOwner.AndroidAuto ? "Android Auto" : "Alarm";
                logger.Information("Stopping {OwnerType} foreground service before MediaElement starts", ownerType);
                var serviceToStop = state.AndroidAutoService ?? state.AlarmService;
                ForegroundServiceOperations.StopForeground(serviceToStop);

                // Clean up alarm service if it was used
                if (state.AlarmService != null)
                {
                    logger.Debug("Cleaning up alarm service after stopping foreground");
                    state.ClearAlarmService();
                }

                needsDelay = true;
            }

            // MediaElement's MediaControlsService will start itself via MediaManager.StartService()
            state.SetOwner(ForegroundServiceOwner.MediaElement);
            logger.Information("Foreground service ownership transferred to MediaElement");
        }

        // Delay outside the lock to ensure Android Auto foreground service is fully stopped
        if (needsDelay)
        {
            await Task.Delay(50);
            logger.Information("Android Auto foreground stopped - MediaElement can now start");
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

            // Clean up alarm service if it exists (alarm foreground service should have been stopped when playback started)
            if (state.AlarmService != null)
            {
                logger.Debug("Cleaning up alarm service reference");
                state.ClearAlarmService();
            }
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

            // Skip if app is already in foreground or has active foreground service
            if (ForegroundServiceValidator.ShouldSkipForegroundServiceStart(state))
            {
                logger.Information("Skipping Android Auto foreground service start - app is in foreground or has active foreground service");
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
    /// Gets the current foreground service owner in a thread-safe manner.
    /// </summary>
    public static ForegroundServiceOwner CurrentOwner
    {
        get
        {
            lock (@lock)
            {
                return state.CurrentOwner;
            }
        }
    }

    /// <summary>
    /// Gets whether Android Auto is currently connected in a thread-safe manner.
    /// </summary>
    public static bool IsAndroidAutoConnected
    {
        get
        {
            lock (@lock)
            {
                return state.IsAndroidAutoConnected;
            }
        }
    }

    /// <summary>
    /// Called when alarm is triggered. Starts foreground service immediately to prevent OS from killing the app
    /// during bootstrap, download, and playback preparation. Will switch to MediaElement when playback starts.
    /// Skips if app is already in foreground or has active foreground service.
    /// </summary>
    /// <param name="context">The Android context (from BroadcastReceiver or Activity)</param>
    /// <param name="scheduleId">The schedule ID for the alarm</param>
    public static async Task OnAlarmTriggered(Context context, int scheduleId)
    {
        Intent? intent = null;

        lock (@lock)
        {
            // Skip if app is already in foreground or has active foreground service
            if (ForegroundServiceValidator.ShouldSkipForegroundServiceStart(state))
            {
                logger.Information("Alarm triggered (schedule {ScheduleId}) but app is in foreground or has active foreground service - skipping foreground service start", scheduleId);
                return;
            }

            logger.Information("Alarm triggered (schedule {ScheduleId}) - starting foreground service to prevent OS kill", scheduleId);
        }

        // Start the AlarmForegroundService (outside lock to avoid blocking)
        intent = new Intent(context, typeof(AlarmForegroundService));

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }

            // Wait for service to be created and registered (thread-safe access via property)
            var maxWait = 10;
            var waitCount = 0;
            while (AlarmForegroundService.Instance == null && waitCount < maxWait)
            {
                await Task.Delay(50);
                waitCount++;
            }

            var service = AlarmForegroundService.Instance;
            if (service == null)
            {
                logger.Warning("AlarmForegroundService instance not available after starting");
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to start AlarmForegroundService");
            return;
        }

        // Get MediaSession and request foreground ownership (re-acquire lock)
        Service? serviceInstance = null;
        lock (@lock)
        {
            // Double-check foreground state after delay (app might have come to foreground)
            if (ForegroundServiceValidator.ShouldSkipForegroundServiceStart(state))
            {
                logger.Information("Alarm foreground service started but app came to foreground - stopping service");
                if (intent != null)
                {
                    context.StopService(intent);
                }
                return;
            }

            // Get service instance once while holding lock to ensure consistency
            serviceInstance = AlarmForegroundService.Instance;
            if (serviceInstance == null)
            {
                logger.Warning("AlarmForegroundService instance became null after lock acquisition");
                if (intent != null)
                {
                    context.StopService(intent);
                }
                return;
            }
        }

        // Get MediaSession and start foreground (service instance captured, safe to use outside lock for this operation)
        // MediaSession is created before bootstrap (in AlarmRingerReceiver), so it may not have metadata yet.
        // ForegroundNotificationHelper.CreateNotification handles alarm notifications with "Preparing playback..." message.
        // The notification will be updated when metadata is set later via UpdateForeground.
        var mediaSession = MediaSessionHelper.Create();
        if (mediaSession != null && serviceInstance != null)
        {
            lock (@lock)
            {
                // Final check before committing state change
                if (ForegroundServiceValidator.ShouldSkipForegroundServiceStart(state))
                {
                    logger.Information("Alarm foreground service ready but app came to foreground - stopping service");
                    if (intent != null)
                    {
                        context.StopService(intent);
                    }
                    return;
                }

                state.SetAlarmService(serviceInstance);
                ForegroundServiceOperations.StartForeground(serviceInstance, mediaSession, isAlarmNotification: true);
                state.SetOwner(ForegroundServiceOwner.Alarm);
                logger.Information("Alarm foreground service started with preparing notification - will switch to MediaElement when playback starts");
            }
        }
        else
        {
            logger.Warning("Failed to get MediaSession or service instance for alarm foreground service");
            if (intent != null)
            {
                context.StopService(intent);
            }
        }
    }

    /// <summary>
    /// Stops the alarm foreground service if it's currently active.
    /// Called when "play only when I tap on notification" is enabled.
    /// 
    /// It's safe to stop the foreground service here because:
    /// - A tappable notification is shown to the user
    /// - When the user taps the notification, the app is brought to foreground
    /// - MediaElement will automatically request foreground service ownership when playback starts
    /// - This allows the app to go to background after showing the notification, saving resources
    /// </summary>
    public static void StopAlarmForegroundServiceIfActive()
    {
        Service? alarmServiceToStop = null;
        lock (@lock)
        {
            if (state.AlarmService != null && state.CurrentOwner == ForegroundServiceOwner.Alarm)
            {
                logger.Information("Stopping alarm foreground service - NotificationEnabled is true, waiting for user tap. MediaElement will handle foreground service when playback starts.");
                alarmServiceToStop = state.AlarmService;
                ForegroundServiceOperations.StopForeground(state.AlarmService);
                state.SetOwner(ForegroundServiceOwner.None);
                state.ClearAlarmService();
            }
        }

        // Stop the service itself (outside lock to avoid blocking)
        if (alarmServiceToStop != null)
        {
            try
            {
                alarmServiceToStop.StopSelf();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error stopping AlarmForegroundService");
            }
        }
    }
}
