#nullable enable
using _Microsoft.Android.Resource.Designer;
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
using Java.Util;
using Serilog;
using Color = Android.Graphics.Color;

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
[IntentFilter(["android.media.browse.MediaBrowserService"])]
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
    // Reuse shared validation logic from AndroidAutoHostValidator
    static bool IsCarHostPackage(string clientPackageName)
    {
        return AndroidAutoHostValidator.IsCarHostPackage(clientPackageName);
    }

    public override void OnCreate()
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            AndroidAutoMediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "LegacyMediaBrowserService.OnCreate: failed to create MediaSession");
        }

        base.OnCreate();
        logger.Information("LegacyMediaBrowserService.OnCreate() called - Ensuring MauiApp is created");

        InitializeMediaSession();
        InitializeBootstrapInBackground();
    }

    private void InitializeMediaSession()
    {
        try
        {
            MauiAppHolder.CreateAndStore();
            SetupMediaSessionManagerAndToken();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing MediaSession in LegacyMediaBrowserService - will retry when service is bound");
        }
    }

    private void SetupMediaSessionManagerAndToken()
    {
        // MediaSession is created here if needed (for SessionToken), but buffering state is set
        // centrally after bootstrap completes in CommonBootstrapHelper.InitializeSchedules().
        mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
        if (mediaSessionManager == null)
        {
            logger.Warning("MediaSessionManager is null - cannot create MediaSession");
            return;
        }

        session = mediaSessionManager.GetOrCreate();
        if (session == null)
        {
            logger.Error("MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken");
            return;
        }

        if (session.SessionToken == null)
        {
            logger.Error("MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized");
            return;
        }

        SessionToken = session.SessionToken;
        logger.Information("SessionToken successfully set: {Token}", SessionToken?.ToString() ?? "null");
        logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Legacy Android Auto is connecting! SessionToken set correctly.");
    }

    private void InitializeBootstrapInBackground()
    {
        _ = Task.Run(() =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Bootstrap initialization started");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InitializeStateSubscriptionAsync();
                        // SetCarPlayScreenAction will be dispatched after bootstrap completes (handled by CommonBootstrapHelper)
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

    private async Task InitializeStateSubscriptionAsync()
    {
        try
        {
            await MauiProgram.WaitForBootstrapAsync(timeoutMs: 30000);
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, "Bootstrap timed out in LegacyMediaBrowserService.OnCreate - will retry when schedules are loaded");
            return;
        }

        applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
        if (applicationState != null)
        {
            scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
            scheduleChangeTracker.Initialize(applicationState);
            applicationState.StateChanged += OnApplicationStateChanged;
            logger.Information("✅ LegacyMediaBrowserService subscribed to schedule list changes");
        }
        else
        {
            logger.Warning("IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI");
        }
    }

    public override BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
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
        return new BrowserRoot(RootId, null);
    }

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (scheduleChangeTracker == null)
            {
                logger.Debug("OnApplicationStateChanged: scheduleChangeTracker is null, skipping");
                return;
            }

            logger.Debug("OnApplicationStateChanged: Checking for schedule changes");
            var changes = scheduleChangeTracker.GetSpecificChanges();
            if (changes == null || changes.Count == 0)
            {
                logger.Debug("OnApplicationStateChanged: No changes detected (changes is null or empty)");
                return;
            }

            logger.Information("OnApplicationStateChanged: Detected {Count} schedule changes, notifying Android Auto", changes.Count);
            var options = CreateChangeNotificationOptions(changes);
            NotifyChildrenChanged(RootId, options);
            LogScheduleChanges(changes);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking schedule list changes - falling back to full refresh");
            PerformFallbackRefresh();
        }
    }

    private Bundle CreateChangeNotificationOptions(List<ScheduleChange> changes)
    {
        var options = new Bundle();
        var changedIds = new List<int>();
        var addedIds = new List<int>();
        var removedIds = new List<int>();

        foreach (var change in changes)
        {
            changedIds.Add(change.ScheduleId);
            CategorizeChange(change, addedIds, removedIds);
        }

        options.PutIntArray("changed_schedule_ids", changedIds.ToArray());
        options.PutIntArray("added_schedule_ids", addedIds.ToArray());
        options.PutIntArray("removed_schedule_ids", removedIds.ToArray());

        return options;
    }

    private void CategorizeChange(ScheduleChange change, List<int> addedIds, List<int> removedIds)
    {
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

    private void LogScheduleChanges(List<ScheduleChange> changes)
    {
        var addedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Added);
        var updatedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Updated);
        var removedCount = changes.Count(c => c.ChangeType == ScheduleChangeType.Removed);

        logger.Debug("Notified Android Auto of schedule changes: {ChangeCount} changes ({AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed)",
            changes.Count, addedCount, updatedCount, removedCount);
    }

    private void PerformFallbackRefresh()
    {
        try
        {
            NotifyChildrenChanged(RootId);
        }
        catch (Exception fallbackEx)
        {
            logger.Error(fallbackEx, "Error performing fallback full refresh");
        }
    }

    public override void OnLoadChildren(string parentId, Result result)
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

    private async Task LoadChildrenAsync(string parentId, Result result)
    {
        try
        {
            logger.Debug("Starting schedule loading for parent: {ParentId}", parentId);

            if (!await WaitForBootstrapAsync(parentId))
            {
                result.SendResult(new ArrayList());
                return;
            }

            var scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
            if (scheduleItems.Count == 0)
            {
                logger.Warning("No schedules found in state for parent: {ParentId} - state may not be initialized yet", parentId);
                result.SendResult(new ArrayList());
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

    private static async Task<bool> WaitForBootstrapAsync(string parentId)
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

    private ArrayList CreateMediaItemsFromSchedules(List<ScheduleStateItem> scheduleItems)
    {
        var mediaItems = new ArrayList();

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

    private static string BuildScheduleDescription(ScheduleStateItem scheduleItem)
    {
        var description = $"Schedule ID: {scheduleItem.Id}";
        if (!string.IsNullOrWhiteSpace(scheduleItem.BibleReadingPublicationCode))
        {
            description += $", {scheduleItem.BibleReadingPublicationCode}";
        }
        return description;
    }

    private MediaDescriptionCompat? CreateMediaDescription(ScheduleStateItem scheduleItem, string title, string subtitle, string description) => CreateMediaDescriptionForSchedule(scheduleItem);

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

    private Bitmap? CreateBookIconBitmap()
    {
        try
        {
            var bookDrawable = GetBookDrawable();
            if (bookDrawable == null)
            {
                return null;
            }

            var bitmap = CreateIconBitmap();
            var canvas = new Canvas(bitmap);

            DrawBookIcon(canvas, bookDrawable);

            logger.Debug("Created book icon bitmap - Size: {Size}x{Size}",
                GetBitmapSize(), GetBitmapSize());
            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create book icon bitmap - MediaItems will display without icon");
            return null;
        }
    }

    private Drawable? GetBookDrawable()
    {
        var bookDrawable = ContextCompat.GetDrawable(this, ResourceConstant.Drawable.ic_book_open);
        if (bookDrawable == null)
        {
            logger.Warning("Could not get app drawable for book icon");
        }
        return bookDrawable;
    }

    private Bitmap CreateIconBitmap()
    {
        int BitmapSize = GetBitmapSize();
        var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
        var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, config);
        bitmap.EraseColor(Color.Transparent);
        return bitmap;
    }

    private void DrawBookIcon(Canvas canvas, Drawable bookDrawable)
    {
        int BookIconSize = GetBookIconSize();
        int BookOffset = BookIconSize / 2 - 8;
        bookDrawable.SetBounds(BookOffset, BookOffset, BookOffset + BookIconSize, BookOffset + BookIconSize);
        bookDrawable.Draw(canvas);
    }

    private static int GetBitmapSize()
    {
        const int BookIconSize = 128;
        const int BookOffset = BookIconSize / 2 - 8;
        return BookIconSize + BookOffset;
    }

    private static int GetBookIconSize()
    {
        return 128;
    }

    private static void SendEmptyResultSafely(Result result, string parentId)
    {
        try
        {
            // Always send a result, even if empty, to satisfy Android's requirement
            // This prevents the IllegalStateException from occurring
            result.SendResult(new ArrayList());
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
        LogBindIntent(intent);
        EnsureServicePersistence();
        EnsureSessionTokenIsSet();

        return base.OnBind(intent);
    }

    private void LogBindIntent(Intent? intent)
    {
        logger.Information("✅ LegacyMediaBrowserService.OnBind() called with intent: {Action}",
            intent?.Action);

        if (intent != null)
        {
            logger.Information("Intent component: {Component}, Package: {Package}, Categories: {Categories}",
                intent.Component?.ClassName, intent.Package, string.Join(", ", intent.Categories ?? Array.Empty<string>()));
        }
    }

    private void EnsureServicePersistence()
    {
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
    }

    private void EnsureSessionTokenIsSet()
    {
        if (SessionToken == null)
        {
            try
            {
                mediaSessionManager ??= ServiceProviderManager.GetService<MediaSessionManager>();
                if (mediaSessionManager != null)
                {
                    session ??= mediaSessionManager.GetOrCreate();
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

}

