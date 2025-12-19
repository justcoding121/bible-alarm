#nullable enable
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Media;
using AndroidX.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// MediaBrowserService for Android Auto - Mandatory backend for all Android Auto versions.
/// 
/// Purpose:
/// - Primary interface for older Android Auto versions (phone projection, old DHU, 2016–2024 cars)
/// - Backend for voice commands, recommendations, and playback controls in newer versions
/// - Works alongside CarAppService: CarAppService handles UI, this handles playback/voice
/// 
/// Strategy: Dual Support
/// - CarAppService: Handles templated UI for browsing and playback screens (CAL API 8+)
/// - MediaBrowserService: Mandatory backend for voice commands, recommendations, and playback controls
/// 
/// Uses the shared MediaSessionCompat from MediaSessionManager to ensure seamless playback continuity
/// across both services.
/// </summary>
[Service(Exported = true)]
[IntentFilter(new[] { "android.media.browse.MediaBrowserService" })]
[Register("bible.alarm.platforms.android.services.androidauto.LegacyMediaBrowserService")]
public class LegacyMediaBrowserService : MediaBrowserServiceCompat
{
    private static readonly ILogger logger = Log.ForContext<LegacyMediaBrowserService>();
    private MediaSessionCompat? _session;
    private MediaSessionManager? _mediaSessionManager;
    private IState<ApplicationState>? _applicationState;
    private AndroidAutoScheduleChangeTracker? _scheduleChangeTracker;
    private const string RootId = "__ID_ROOT__";

    // Samsung/phone SystemUI may bind to any exported MediaBrowserService and show a phone media card.
    // We only want car hosts (Android Auto / AAOS) to connect to this service.
    static bool IsCarHostPackage(string clientPackageName)
    {
        if (string.IsNullOrWhiteSpace(clientPackageName))
        {
            return false;
        }

        // Android Auto (phone projection)
        if (clientPackageName.Equals("com.google.android.projection.gearhead", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Emulator DHU / Google automotive projection variants sometimes use these prefixes
        if (clientPackageName.Contains("car", StringComparison.OrdinalIgnoreCase)
            || clientPackageName.Contains("auto", StringComparison.OrdinalIgnoreCase)
            || clientPackageName.StartsWith("com.google.android.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public override void OnCreate()
    {
        base.OnCreate();

        logger.Information("LegacyMediaBrowserService.OnCreate() called - Ensuring MauiApp is created");

        // Create the DI container immediately (fast) so ServiceProviderManager is available synchronously.
        // Then publish a blank, non-interactive loading UI to Android Auto ASAP.
        try
        {
            MauiAppHolder.CreateAndStore();

            // Get MediaSessionManager from service provider (now available after CreateAndStore)
            _mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
            if (_mediaSessionManager == null)
            {
                logger.Warning("MediaSessionManager is null - cannot create MediaSession");
                return;
            }

            _session = _mediaSessionManager.GetOrCreate(true);
            if (_session == null)
            {
                logger.Error("MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken");
                return;
            }

            // Verify SessionToken is available before setting it
            if (_session.SessionToken == null)
            {
                logger.Error("MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized");
                return;
            }

            // THIS IS THE KEY LINE — both systems now see the same session
            SessionToken = _session.SessionToken;

            logger.Information("SessionToken successfully set: {Token}", SessionToken?.ToString() ?? "null");
            logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Legacy Android Auto is connecting! SessionToken set correctly.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing MediaSession in LegacyMediaBrowserService - will retry when service is bound");
        }

        // Ensure bootstrap is initialized in the background (long-running).
        // This will load schedules and eventually overwrite the loading UI with real metadata.
        _ = Task.Run(() =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Bootstrap initialization started");

                // Subscribe to state changes after bootstrap is initialized
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait for bootstrap to complete before accessing state
                        // Use a longer timeout for OnCreate since it's not blocking the UI
                        try
                        {
                            await MauiProgram.WaitForBootstrapAsync(timeoutMs: 30000);
                        }
                        catch (Exception bootstrapEx)
                        {
                            logger.Warning(bootstrapEx, "Bootstrap timed out in LegacyMediaBrowserService.OnCreate - will retry when schedules are loaded");
                            // Don't throw - allow service to continue, schedules will be loaded when bootstrap completes
                            return;
                        }

                        _applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
                        if (_applicationState != null)
                        {
                            // Initialize schedule change tracker
                            _scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
                            _scheduleChangeTracker.Initialize(_applicationState);

                            _applicationState.StateChanged += OnApplicationStateChanged;
                            logger.Information("✅ LegacyMediaBrowserService subscribed to schedule list changes");
                        }
                        else
                        {
                            logger.Warning("IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI");
                        }

                        // Set metadata to first schedule after bootstrap completes
                        await SetInitialScheduleMetadataAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error subscribing to ApplicationState changes in LegacyMediaBrowserService");
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing bootstrap in LegacyMediaBrowserService");
            }
        });
    }

    public override MediaBrowserServiceCompat.BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        logger.Information("✅ OnGetRoot called for client: {ClientPackageName} (UID: {ClientUid})",
            clientPackageName, clientUid);

        // Block non-car clients (e.g., Samsung SystemUI) from binding and generating a phone media card.
        if (!IsCarHostPackage(clientPackageName))
        {
            logger.Information("Rejecting MediaBrowser client (non-car host): {ClientPackageName}", clientPackageName);
            return null;
        }

        // Standard media root ID for Android Auto/AAOS compatibility
        // The root ID "__ID_ROOT__" is a common practice for media apps
        // Android Auto and AAOS hosts are trusted by default when connecting to MediaBrowserService
        return new MediaBrowserServiceCompat.BrowserRoot(RootId, null);
    }

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (_scheduleChangeTracker == null)
            {
                return;
            }

            // Check if schedules changed using the shared tracker
            if (_scheduleChangeTracker.CheckForChanges())
            {
                logger.Debug("Schedule list changed - notifying Android Auto that children list has changed");

                // Notify Android Auto that the children list has changed so it reloads the schedule list
                NotifyChildrenChanged(RootId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking schedule list changes");
        }
    }

    public override void OnLoadChildren(string parentId, MediaBrowserServiceCompat.Result result)
    {
        logger.Information("✅ OnLoadChildren called for parent: {ParentId}", parentId);

        try
        {
            // CRITICAL: Detach the result before starting async work
            // Android requires that OnLoadChildren either calls SendResult() synchronously
            // or calls Detach() before returning if the result will be sent asynchronously
            result.Detach();
            logger.Debug("Result detached successfully for parent: {ParentId}", parentId);

            // Run work to load schedules and create MediaItems on background thread
            // Bootstrap check is done asynchronously inside Task.Run to avoid blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.Debug("Starting schedule loading for parent: {ParentId}", parentId);

                    // Check if bootstrap is ready - if not, wait with timeout
                    // If bootstrap times out or isn't ready, return empty list gracefully
                    try
                    {
                        await MauiProgram.WaitForBootstrapAsync(timeoutMs: 5000); // Short timeout for responsiveness
                        logger.Debug("Bootstrap completed, loading schedules from state for parent: {ParentId}", parentId);
                    }
                    catch (Exception bootstrapEx)
                    {
                        logger.Warning(bootstrapEx, "Bootstrap not ready or timed out for parent: {ParentId} - returning empty list", parentId);
                        result.SendResult(new Java.Util.ArrayList());
                        return;
                    }

                    // Load schedule state items from state using shared helper
                    var scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();

                    if (scheduleItems.Count == 0)
                    {
                        logger.Warning("No schedules found in state for parent: {ParentId} - state may not be initialized yet", parentId);
                        result.SendResult(new Java.Util.ArrayList());
                        return;
                    }

                    // Convert schedules to MediaBrowserCompat.MediaItem objects
                    var mediaItems = new Java.Util.ArrayList();

                    foreach (var scheduleItem in scheduleItems)
                    {
                        try
                        {
                            // Use shared helper to build title and subtitle from ScheduleStateItem DTO
                            var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
                            var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);

                            // Create MediaDescriptionCompat for each schedule
                            // Note: MediaDescriptionCompat is in Android.Support.V4.Media namespace
                            // Set mediaId to just the schedule ID (not "schedule_{id}") so OnPlayFromMediaId can parse it as int
                            var descriptionBuilder = new MediaDescriptionCompat.Builder();
                            descriptionBuilder.SetMediaId(scheduleItem.Id.ToString());
                            descriptionBuilder.SetTitle(title);
                            descriptionBuilder.SetSubtitle(subtitle);

                            // Set description with schedule details
                            var description = $"Schedule ID: {scheduleItem.Id}";
                            if (!string.IsNullOrWhiteSpace(scheduleItem.BibleReadingPublicationCode))
                            {
                                description += $", {scheduleItem.BibleReadingPublicationCode}";
                            }
                            descriptionBuilder.SetDescription(description);

                            // Add headphone/audio icon to beautify the playlist
                            var iconBitmap = CreateHeadphoneIconBitmap();
                            if (iconBitmap != null)
                            {
                                descriptionBuilder.SetIconBitmap(iconBitmap);
                            }

                            var mediaDescription = descriptionBuilder.Build();

                            // Create MediaBrowserCompat.MediaItem with FLAG_PLAYABLE flag
                            // This indicates the item can be played when selected
                            // Note: MediaBrowserCompat is in Android.Support.V4.Media namespace
                            if (mediaDescription != null)
                            {
                                var mediaItem = new MediaBrowserCompat.MediaItem(
                                    mediaDescription,
                                    MediaBrowserCompat.MediaItem.FlagPlayable);

                                mediaItems.Add(mediaItem);
                            }

                            logger.Debug("Added MediaItem for schedule: {ScheduleId} - Title: {Title}, Subtitle: {Subtitle}",
                                scheduleItem.Id, title, mediaDescription?.Subtitle ?? "");
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "Failed to create MediaItem for schedule {ScheduleId}", scheduleItem.Id);
                        }
                    }

                    logger.Information("Created {Count} MediaItems for Android Auto", mediaItems.Size());
                    result.SendResult(mediaItems);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error loading children in LegacyMediaBrowserService for parent: {ParentId}. Bootstrap may not have completed or services may not be available.", parentId);
                    try
                    {
                        // Always send a result, even if empty, to satisfy Android's requirement
                        // This prevents the IllegalStateException from occurring
                        result.SendResult(new Java.Util.ArrayList());
                        logger.Debug("Sent empty result after error for parent: {ParentId}", parentId);
                    }
                    catch (Exception sendEx)
                    {
                        logger.Error(sendEx, "Failed to send empty result after error for parent: {ParentId}. This may cause Android Auto connection issues.", parentId);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // If Detach() itself fails, try to send empty result synchronously
            logger.Error(ex, "Critical error in OnLoadChildren before detaching result for parent: {ParentId}", parentId);
            try
            {
                result.SendResult(new Java.Util.ArrayList());
            }
            catch (Exception sendEx)
            {
                logger.Error(sendEx, "Failed to send result synchronously after critical error for parent: {ParentId}", parentId);
            }
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Handle media button events (steering wheel buttons, etc.)
        if (_session != null)
        {
            MediaButtonReceiver.HandleIntent(_session, intent);
        }
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        logger.Information("✅ LegacyMediaBrowserService.OnBind() called with intent: {Action}",
            intent?.Action);

        // Log the intent details to help debug binder conflicts
        if (intent != null)
        {
            logger.Information("Intent component: {Component}, Package: {Package}, Categories: {Categories}",
                intent.Component?.ClassName, intent.Package, string.Join(", ", intent.Categories ?? Array.Empty<string>()));
        }

        // CRITICAL: Start the service to keep it alive even if Android Auto temporarily unbinds
        // MediaBrowserServiceCompat is a bound service, so Android can destroy it when all clients unbind.
        // By starting it as a sticky service, we ensure it persists across temporary unbind events.
        // This prevents the service from being destroyed while Android Auto is still in proximity.
        try
        {
            var startIntent = new Intent(this, typeof(LegacyMediaBrowserService));
            StartService(startIntent);
            logger.Debug("Service started to keep it alive during Android Auto connection");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to start service in OnBind() - service may be destroyed if Android Auto unbinds");
        }

        // If SessionToken wasn't set in OnCreate() (bootstrap may not have completed), try to set it now
        if (SessionToken == null)
        {
            try
            {
                _mediaSessionManager ??= ServiceProviderManager.GetService<MediaSessionManager>();
                if (_mediaSessionManager != null)
                {
                    _session ??= _mediaSessionManager.GetOrCreate(true);
                    if (_session?.SessionToken != null)
                    {
                        SessionToken = _session.SessionToken;
                        logger.Information("SessionToken set in OnBind(): {Token}", SessionToken?.ToString() ?? "null");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not set SessionToken in OnBind() - bootstrap may still be running");
            }
        }

        return base.OnBind(intent);
    }

    public override bool OnUnbind(Intent? intent)
    {
        logger.Information("⚠️ LegacyMediaBrowserService.OnUnbind() called - Client disconnected");

        // Note: Even though a client unbinds, the service may not be destroyed immediately
        // because we start it as a sticky service in OnBind(). This prevents premature destruction
        // when Android Auto temporarily disconnects and reconnects.
        // The service will only be destroyed if StopService() is explicitly called or the system
        // needs to reclaim resources (which is rare for sticky services).

        return base.OnUnbind(intent);
    }

    /// <summary>
    /// Creates a bitmap icon for headphone/audio playback to display in Android Auto playlist.
    /// Uses Android's standard media play icon as a headphone representation.
    /// </summary>
    private Bitmap? CreateHeadphoneIconBitmap()
    {
        try
        {
            // Use Android's built-in media play icon (android.R.drawable.ic_media_play)
            // This provides a consistent, recognizable icon for audio content in Android Auto
            var drawable = global::Android.Content.Res.Resources.System?.GetDrawable(global::Android.Resource.Drawable.IcMediaPlay, null);
            if (drawable == null)
            {
                logger.Warning("Could not get Android system drawable for headphone icon");
                return null;
            }

            // Convert drawable to bitmap
            if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
            {
                return bitmapDrawable.Bitmap;
            }

            // If not a BitmapDrawable, create a bitmap from the drawable
            var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
            var bitmap = Bitmap.CreateBitmap(
                drawable.IntrinsicWidth > 0 ? drawable.IntrinsicWidth : 64,
                drawable.IntrinsicHeight > 0 ? drawable.IntrinsicHeight : 64,
                config);

            var canvas = new Canvas(bitmap);
            drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
            drawable.Draw(canvas);

            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create headphone icon bitmap - MediaItems will display without icon");
            return null;
        }
    }

    public override void OnDestroy()
    {
        logger.Information("✅ LegacyMediaBrowserService destroyed - Clearing local MediaSession reference");

        // Unsubscribe from state changes
        try
        {
            if (_applicationState != null)
            {
                _applicationState.StateChanged -= OnApplicationStateChanged;
                logger.Debug("LegacyMediaBrowserService unsubscribed from ApplicationState changes");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error unsubscribing from ApplicationState changes in LegacyMediaBrowserService");
        }

        // IMPORTANT: Do NOT release the shared MediaSessionCompat here!
        // The MediaSession is managed by MediaSessionManager as a singleton and must persist
        // across service lifecycle changes. Android Auto expects the MediaSession to remain
        // available even when the service is temporarily destroyed and recreated.
        // Only clear the local reference - MediaSessionManager will handle cleanup when appropriate.
        _session = null;

        base.OnDestroy();
    }

    private async Task SetInitialScheduleMetadataAsync()
    {
        try
        {
            logger.Debug("SetInitialScheduleMetadataAsync: Setting metadata to first schedule after bootstrap");

            var defaultScheduleService = ServiceProviderManager.GetService<IDefaultScheduleService>();
            if (defaultScheduleService == null)
            {
                logger.Warning("SetInitialScheduleMetadataAsync: IDefaultScheduleService not available");
                return;
            }

            var metadata = await defaultScheduleService.GetNextScheduleTrackMetaDataAsync();

            // Update metadata on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_mediaSessionManager == null)
                {
                    logger.Warning("SetInitialScheduleMetadataAsync: MediaSessionManager is null");
                    return;
                }

                // Set metadata using MediaSessionManager
                _mediaSessionManager.UpdateMetadata(
                    metadata.Title,
                    metadata.Artist,
                    metadata.Album,
                    metadata.ScheduleId);

                // Return to stopped state (idle) with normal actions once metadata is ready.
                _mediaSessionManager.UpdatePlaybackStateForStop();

                logger.Information("SetInitialScheduleMetadataAsync: Set metadata to first schedule - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}",
                    metadata.ScheduleId, metadata.Title, metadata.Artist);
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting initial schedule metadata in LegacyMediaBrowserService");
        }
    }
}

