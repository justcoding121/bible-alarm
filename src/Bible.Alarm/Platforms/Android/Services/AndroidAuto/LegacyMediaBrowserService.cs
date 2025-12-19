#nullable enable
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;
using AndroidX.Media;
using AndroidX.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
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
    private MediaSessionCompat? session;
    private MediaSessionManager? mediaSessionManager;
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;
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
            mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
            if (mediaSessionManager == null)
            {
                logger.Warning("MediaSessionManager is null - cannot create MediaSession");
                return;
            }

            session = mediaSessionManager.GetOrCreate(true);
            if (session == null)
            {
                logger.Error("MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken");
                return;
            }

            // Verify SessionToken is available before setting it
            if (session.SessionToken == null)
            {
                logger.Error("MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized");
                return;
            }

            // THIS IS THE KEY LINE — both systems now see the same session
            SessionToken = session.SessionToken;

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

                        applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
                        if (applicationState != null)
                        {
                            // Initialize schedule change tracker
                            scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
                            scheduleChangeTracker.Initialize(applicationState);

                            applicationState.StateChanged += OnApplicationStateChanged;
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
        // 
        // DEFAULT RECOMMENDATIONS: We return null for rootHints to use default behavior.
        // Android Auto will automatically pull the top items from the root browse tree for "For You" recommendations.
        // The system reuses the exact MediaDescriptionCompat for those items, including their icons.
        // We do NOT provide explicit recommendations via EXTRA_SUGGESTED to keep it simple and let the system handle it.
        return new MediaBrowserServiceCompat.BrowserRoot(RootId, null);
    }

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (scheduleChangeTracker == null)
            {
                return;
            }

            // Get specific changes to update only affected items
            var changes = scheduleChangeTracker.GetSpecificChanges();
            if (changes == null || changes.Count == 0)
            {
                return;
            }

            // MediaBrowserServiceCompat doesn't support true item-level updates,
            // but we can optimize by only notifying when specific items change
            // and include metadata about which items changed in the Bundle
            var options = new Bundle();
            var changedIds = new List<int>();
            var addedIds = new List<int>();
            var removedIds = new List<int>();

            foreach (var change in changes)
            {
                changedIds.Add(change.ScheduleId);
                switch (change.ChangeType)
                {
                    case ScheduleChangeType.Added:
                        addedIds.Add(change.ScheduleId);
                        logger.Debug("Detected schedule added: {ScheduleId}", change.ScheduleId);
                        break;
                    case ScheduleChangeType.Removed:
                        removedIds.Add(change.ScheduleId);
                        logger.Debug("Detected schedule removed: {ScheduleId}", change.ScheduleId);
                        break;
                    case ScheduleChangeType.Updated:
                        logger.Debug("Detected schedule updated: {ScheduleId}", change.ScheduleId);
                        break;
                }
            }

            // Store change metadata in Bundle for potential future use
            // Android Auto will reload the list, but we've optimized by only notifying when specific items change
            options.PutIntArray("changed_schedule_ids", changedIds.ToArray());
            options.PutIntArray("added_schedule_ids", addedIds.ToArray());
            options.PutIntArray("removed_schedule_ids", removedIds.ToArray());

            // Notify Android Auto that children have changed
            // CRITICAL FIX: Calling NotifyChildrenChanged(RootId) when GetSpecificChanges() detects a removal
            // forces the Android Auto recommendation engine to flush its cache for the root.
            // This fixes the "phantom" deleted playlist issue in "For You" cards.
            // The system re-queries the root and sees the item is gone.
            // 
            // We call this for all change types (added/removed/updated) to keep the list fresh,
            // but the key benefit is that removals trigger the cache flush, eliminating phantom items.
            // Note: MediaBrowserServiceCompat will reload the entire list, but this is more efficient
            // than calling NotifyChildrenChanged on every state change
            NotifyChildrenChanged(RootId, options);
            logger.Debug("Notified Android Auto of schedule changes: {ChangeCount} changes ({AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed)",
                changes.Count, addedIds.Count, changes.Count(c => c.ChangeType == ScheduleChangeType.Updated), removedIds.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking schedule list changes - falling back to full refresh");
            // Fallback to full refresh if item-level updates fail
            try
            {
                NotifyChildrenChanged(RootId);
            }
            catch (Exception fallbackEx)
            {
                logger.Error(fallbackEx, "Error performing fallback full refresh");
            }
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

            // DEFAULT RECOMMENDATIONS: We do NOT check for EXTRA_SUGGESTED in rootHints.
            // We always return all items from the root browse tree.
            // Android Auto will automatically use the top items from this list for "For You" recommendations,
            // reusing the exact MediaDescriptionCompat (including icons) for those items.
            // This is simpler and more maintainable than providing explicit recommendations.

            // Run work to load schedules and create MediaItems on background thread
            _ = Task.Run(async () => await LoadChildrenAsync(parentId, result));
        }
        catch (Exception ex)
        {
            // If Detach() itself fails, try to send empty result synchronously
            logger.Error(ex, "Critical error in OnLoadChildren before detaching result for parent: {ParentId}", parentId);
            SendEmptyResultSafely(result, parentId);
        }
    }

    private async Task LoadChildrenAsync(string parentId, MediaBrowserServiceCompat.Result result)
    {
        try
        {
            logger.Debug("Starting schedule loading for parent: {ParentId}", parentId);

            if (!await WaitForBootstrapAsync(parentId))
            {
                result.SendResult(new Java.Util.ArrayList());
                return;
            }

            var scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
            if (scheduleItems.Count == 0)
            {
                logger.Warning("No schedules found in state for parent: {ParentId} - state may not be initialized yet", parentId);
                result.SendResult(new Java.Util.ArrayList());
                return;
            }

            var mediaItems = CreateMediaItemsFromSchedules(scheduleItems);
            logger.Information("Created {Count} MediaItems for Android Auto", mediaItems.Size());
            result.SendResult(mediaItems);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading children in LegacyMediaBrowserService for parent: {ParentId}. Bootstrap may not have completed or services may not be available.", parentId);
            SendEmptyResultSafely(result, parentId);
        }
    }

    private async Task<bool> WaitForBootstrapAsync(string parentId)
    {
        try
        {
            await MauiProgram.WaitForBootstrapAsync(timeoutMs: 5000); // Short timeout for responsiveness
            logger.Debug("Bootstrap completed, loading schedules from state for parent: {ParentId}", parentId);
            return true;
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, "Bootstrap not ready or timed out for parent: {ParentId} - returning empty list", parentId);
            return false;
        }
    }

    private Java.Util.ArrayList CreateMediaItemsFromSchedules(List<ScheduleStateItem> scheduleItems)
    {
        var mediaItems = new Java.Util.ArrayList();

        foreach (var scheduleItem in scheduleItems)
        {
            try
            {
                var mediaItem = CreateMediaItemFromSchedule(scheduleItem);
                if (mediaItem != null)
                {
                    mediaItems.Add(mediaItem);
                    logger.Debug("Added MediaItem for schedule: {ScheduleId} - Title: {Title}",
                        scheduleItem.Id, AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem));
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to create MediaItem for schedule {ScheduleId}", scheduleItem.Id);
            }
        }

        return mediaItems;
    }

    private MediaBrowserCompat.MediaItem? CreateMediaItemFromSchedule(ScheduleStateItem scheduleItem)
    {
        var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
        var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);
        var description = BuildScheduleDescription(scheduleItem);

        var mediaDescription = CreateMediaDescription(scheduleItem, title, subtitle, description);
        if (mediaDescription == null)
        {
            return null;
        }

        return new MediaBrowserCompat.MediaItem(
            mediaDescription,
            MediaBrowserCompat.MediaItem.FlagPlayable);
    }

    private string BuildScheduleDescription(ScheduleStateItem scheduleItem)
    {
        var description = $"Schedule ID: {scheduleItem.Id}";
        if (!string.IsNullOrWhiteSpace(scheduleItem.BibleReadingPublicationCode))
        {
            description += $", {scheduleItem.BibleReadingPublicationCode}";
        }
        return description;
    }

    private MediaDescriptionCompat? CreateMediaDescription(ScheduleStateItem scheduleItem, string title, string subtitle, string description)
    {
        return CreateMediaDescriptionForSchedule(scheduleItem);
    }

    /// <summary>
    /// Creates a MediaDescriptionCompat for a schedule item.
    /// Used for both initial load and item-level updates.
    /// 
    /// CRITICAL: MediaId must remain stable (use schedule ID, not name).
    /// If MediaId changes, Android Auto treats it as a new item, causing:
    /// - Loss of scroll position
    /// - Loss of "playing" icon indicator
    /// - UI jump/flash
    /// If MediaId stays the same but Title changes, Android Auto updates the text in place.
    /// </summary>
    private MediaDescriptionCompat? CreateMediaDescriptionForSchedule(ScheduleStateItem scheduleItem)
    {
        var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
        var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);
        var description = BuildScheduleDescription(scheduleItem);

        var descriptionBuilder = new MediaDescriptionCompat.Builder();
        // CRITICAL: Use schedule ID (stable) as MediaId, not name (can change)
        // This ensures Android Auto updates items in place when name changes
        descriptionBuilder.SetMediaId(scheduleItem.Id.ToString());
        descriptionBuilder.SetTitle(title);
        descriptionBuilder.SetSubtitle(subtitle);
        descriptionBuilder.SetDescription(description);

        var bookIconBitmap = CreateBookIconBitmap();
        if (bookIconBitmap != null)
        {
            descriptionBuilder.SetIconBitmap(bookIconBitmap);
            logger.Debug("Set book icon bitmap for schedule {ScheduleId} - Size: {Width}x{Height}", 
                scheduleItem.Id, bookIconBitmap.Width, bookIconBitmap.Height);
        }
        else
        {
            logger.Warning("Failed to create book icon bitmap for schedule {ScheduleId}", scheduleItem.Id);
        }

        return descriptionBuilder.Build();
    }

    private void SendEmptyResultSafely(MediaBrowserServiceCompat.Result result, string parentId)
    {
        try
        {
            // Always send a result, even if empty, to satisfy Android's requirement
            // This prevents the IllegalStateException from occurring
            result.SendResult(new Java.Util.ArrayList());
            logger.Debug("Sent empty result for parent: {ParentId}", parentId);
        }
        catch (Exception sendEx)
        {
            logger.Error(sendEx, "Failed to send empty result for parent: {ParentId}. This may cause Android Auto connection issues.", parentId);
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Handle media button events (steering wheel buttons, etc.)
        if (session != null)
        {
            MediaButtonReceiver.HandleIntent(session, intent);
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
                mediaSessionManager ??= ServiceProviderManager.GetService<MediaSessionManager>();
                if (mediaSessionManager != null)
                {
                    session ??= mediaSessionManager.GetOrCreate(true);
                    if (session?.SessionToken != null)
                    {
                        SessionToken = session.SessionToken;
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
    /// Creates a bitmap icon for playlist items to display in Android Auto.
    /// Uses a custom open book icon to represent Bible reading schedules.
    /// Android Auto requires icons to be at least 64x64 pixels for proper display.
    /// </summary>
    private Bitmap? CreateBookIconBitmap()
    {
        try
        {
            // Use custom open book icon from app resources
            // This provides a recognizable icon for Bible reading content in Android Auto
            var drawable = ContextCompat.GetDrawable(this, Resource.Drawable.ic_book_open);
            if (drawable == null)
            {
                logger.Warning("Could not get app drawable for book icon");
                return null;
            }

            // Android Auto requires icons to be at least 64x64 pixels for proper display
            // Use a larger size to ensure good quality on high-DPI displays
            const int IconSize = 128;
            var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
            var bitmap = Bitmap.CreateBitmap(IconSize, IconSize, config);

            // Clear the bitmap with transparent background
            bitmap.EraseColor(global::Android.Graphics.Color.Transparent);

            var canvas = new Canvas(bitmap);
            drawable.SetBounds(0, 0, IconSize, IconSize);
            drawable.Draw(canvas);

            logger.Debug("Created book icon bitmap - Size: {Size}x{Size}", IconSize, IconSize);
            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create book icon bitmap - MediaItems will display without icon");
            return null;
        }
    }

    public override void OnDestroy()
    {
        logger.Information("✅ LegacyMediaBrowserService destroyed - Clearing local MediaSession reference");

        // Unsubscribe from state changes
        try
        {
            if (applicationState != null)
            {
                applicationState.StateChanged -= OnApplicationStateChanged;
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
        session = null;

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
                if (mediaSessionManager == null)
                {
                    logger.Warning("SetInitialScheduleMetadataAsync: MediaSessionManager is null");
                    return;
                }

                // Set metadata using MediaSessionManager
                mediaSessionManager.UpdateMetadata(
                    metadata.Title,
                    metadata.Artist,
                    metadata.Album,
                    metadata.ScheduleId,
                    metadata.ArtworkUrl);

                // Return to stopped state (idle) with normal actions once metadata is ready.
                mediaSessionManager.UpdatePlaybackStateForStop();

                logger.Information("SetInitialScheduleMetadataAsync: Set metadata to first schedule - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}, HasArtwork={HasArtwork}",
                    metadata.ScheduleId, metadata.Title, metadata.Artist, !string.IsNullOrEmpty(metadata.ArtworkUrl));
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting initial schedule metadata in LegacyMediaBrowserService");
        }
    }
}

