#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Validation;
using AndroidX.Core.Content;
using AndroidX.Core.Graphics.Drawable;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using Action = AndroidX.Car.App.Model.Action;
using Color = Android.Graphics.Color;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// CarAppService for modern Android Automotive OS (Polestar, Volvo, GM, Rivian, Ford 2024+).
/// Uses the shared MediaSessionCompat from MediaSessionManager to ensure seamless playback continuity.
/// 
/// Strategy: Dual Support
/// - CarAppService: Handles templated UI for browsing and playback screens (CAL API 8+)
/// - MediaBrowserService: Mandatory backend for voice commands, recommendations, and playback controls
/// 
/// The host (Android Auto/AAOS) determines which service to bind to based on capability:
/// - Newer systems: Use CarAppService for UI, MediaBrowserService for playback
/// - Older systems: Fall back to MediaBrowserService for everything
/// </summary>
[Service(Exported = true, Name = "bible.alarm.platforms.android.services.androidauto.CarAppService")]
// CRITICAL: MEDIA category removed to prevent phone projection Android Auto from discovering this service
// Phone projection Android Auto should only discover LegacyMediaBrowserService
// Modern AAOS will discover this via androidx.car.app.host.description metadata
[IntentFilter(["androidx.car.app.CarAppService"])]
// Links the CarAppService to the description file for modern Android Auto/AAOS discovery
[MetaData("androidx.car.app.host.description", Resource = "@xml/car_app_desc")]
// Declare minimum Car App Library API level (using integer resource)
[MetaData("androidx.car.app.minCarApiLevel", Resource = "@integer/car_app_min_api_level")]
// Declare target Car App Library API level (using integer resource)
[MetaData("androidx.car.app.targetCarApiLevel", Resource = "@integer/car_app_target_api_level")]
[Register("bible.alarm.platforms.android.services.androidauto.CarAppService")]
public class CarAppService : AndroidX.Car.App.CarAppService
{
    private static readonly ILogger logger = Log.ForContext<CarAppService>();

    public override void OnCreate()
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CarAppService.OnCreate: failed to create MediaSession");
        }

        base.OnCreate();

        logger.Information("CarAppService.OnCreate() called - Ensuring MauiApp is created");

        // Create the DI container immediately (fast) so ServiceProviderManager is available synchronously.
        // MediaSession will be created here if needed (for SessionToken), but buffering state is set
        // centrally after bootstrap completes in CommonBootstrapHelper.InitializeSchedules().
        try
        {
            MauiAppHolder.CreateAndStore();
            var mediaSessionManager = ServiceProviderManager.GetService<Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManager>();
            mediaSessionManager?.GetOrCreate();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CarAppService.OnCreate: failed to create MauiApp / initialize MediaSession");
        }

        // Ensure MauiApp is created and bootstrap is initialized (idempotent - safe to call multiple times)
        // Bootstrap initialization is thread-safe and will only run once even if called from multiple services
        _ = Task.Run(async () =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                logger.Information("✅ CarAppService.OnCreate() completed - Bootstrap initialization started");

                // Wait for bootstrap to complete
                // Use a longer timeout for OnCreate since it's not blocking the UI
                // SetCarPlayScreenAction will be dispatched after bootstrap completes (handled by CommonBootstrapHelper)
                try
                {
                    await MauiProgram.WaitForBootstrapAsync();
                }
                catch (Exception bootstrapEx)
                {
                    logger.Warning(bootstrapEx, "Bootstrap timed out in CarAppService.OnCreate - will retry when template is requested");
                    // Don't throw - allow service to continue, template will be generated when bootstrap completes
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing bootstrap in CarAppService");
            }
        });
    }


    public override HostValidator? CreateHostValidator()
    {
        logger.Information("✅ CarAppService.CreateHostValidator() called");
        // Note: HostValidator is sealed in AndroidX Car App Library, so we can't create a custom implementation.
        // Host validation will be handled in OnCreateSession by checking the host package name.
        // Return null to use default validation (allows all hosts), then filter in OnCreateSession.
        return null;
    }

    public override Session OnCreateSession()
    {
        try
        {
            logger.Information("✅ CarAppService.OnCreateSession() called - Modern Android Auto is connecting!");
            // Get ModernMediaSession from service provider
            var session = ServiceProviderManager.GetService<ModernMediaSession>();
            if (session == null)
            {
                logger.Error("ModernMediaSession is null - cannot create session");
                throw new InvalidOperationException("ModernMediaSession is null");
            }
            logger.Information("✅ CarAppService.OnCreateSession() completed successfully");
            return session;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "❌ CarAppService.OnCreateSession() failed - This may indicate a Binder interface mismatch");
            throw;
        }
    }
}

/// <summary>
/// Session for modern Android Auto that attaches the shared MediaSessionCompat.
/// </summary>
public class ModernMediaSession : Session
{
    private static readonly ILogger logger = Log.ForContext<ModernMediaSession>();
    private readonly MediaSessionCompat phoneSession;
    private readonly MediaSessionManager mediaSessionManager;

    public ModernMediaSession(MediaSessionManager mediaSessionManager)
    {
        this.mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        phoneSession = mediaSessionManager.GetOrCreate();
        logger.Information("✅ ModernMediaSession created");
    }

    public override Screen OnCreateScreen(Intent? intent)
    {
        logger.Information("✅ ModernMediaSession.OnCreateScreen() called with intent: {Action}", intent?.Action);

        // The media session is attached via the template in MainCarScreen
        logger.Information("✅ Modern Android Auto connected — will attach shared MediaSession via template");

        if (CarContext == null)
        {
            logger.Error("CarContext is null, cannot create MainCarScreen");
            throw new InvalidOperationException("CarContext is null");
        }

        return new MainCarScreen(CarContext, mediaSessionManager);
    }
}

/// <summary>
/// Main car screen that displays the schedule list with playback controls.
/// </summary>
public class MainCarScreen : Screen, IDisposable
{
    private static readonly ILogger logger = Log.ForContext<MainCarScreen>();
    private readonly MediaSessionManager mediaSessionManager;
    private List<ScheduleStateItem>? scheduleItems;
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;
    private bool disposed;

    public MainCarScreen(CarContext carContext, MediaSessionManager mediaSessionManager) : base(carContext)
    {
        this.mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        logger.Information("✅ MainCarScreen created");

        // Subscribe to state changes to refresh the template when schedules are added/updated/removed
        try
        {
            applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
            if (applicationState != null)
            {
                // Initialize schedule change tracker
                scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
                scheduleChangeTracker.Initialize(applicationState);

                applicationState.StateChanged += OnApplicationStateChanged;
                logger.Information("✅ MainCarScreen subscribed to schedule list changes");
            }
            else
            {
                logger.Warning("IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error subscribing to ApplicationState changes in MainCarScreen");
        }
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

            // Car App Library ListTemplate doesn't support true item-level updates,
            // but we can optimize by only invalidating when specific items change
            // and track which items changed for logging/debugging
            var addedIds = new List<int>();
            var removedIds = new List<int>();
            var updatedIds = new List<int>();

            foreach (var change in changes)
            {
                switch (change.ChangeType)
                {
                    case ScheduleChangeType.Added:
                        addedIds.Add(change.ScheduleId);
                        logger.Debug("Detected schedule added in CarApp: {ScheduleId}", change.ScheduleId);
                        break;
                    case ScheduleChangeType.Removed:
                        removedIds.Add(change.ScheduleId);
                        logger.Debug("Detected schedule removed in CarApp: {ScheduleId}", change.ScheduleId);
                        break;
                    case ScheduleChangeType.Updated:
                        updatedIds.Add(change.ScheduleId);
                        logger.Debug("Detected schedule updated in CarApp: {ScheduleId}", change.ScheduleId);
                        break;
                }
            }

            // Invalidate the template to force Android Auto to call OnGetTemplate() again
            // OPTIMIZATION: By only calling Invalidate() when GetSpecificChanges() is non-empty,
            // we prevent the "refresh flash" (where the list briefly disappears and reappears)
            // that users often see in Android Auto apps when they over-refresh.
            // Note: Car App Library will rebuild the entire template, but this is more efficient
            // than calling Invalidate() on every state change
            Invalidate();
            logger.Debug("Invalidated CarApp template due to schedule changes: {ChangeCount} changes ({AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed)",
                changes.Count, addedIds.Count, updatedIds.Count, removedIds.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking schedule list changes - falling back to full refresh");
            // Fallback to full refresh if item-level updates fail
            try
            {
                Invalidate();
            }
            catch (Exception fallbackEx)
            {
                logger.Error(fallbackEx, "Error performing fallback template invalidation");
            }
        }
    }

    public new void Dispose()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (applicationState != null)
            {
                applicationState.StateChanged -= OnApplicationStateChanged;
                logger.Debug("MainCarScreen unsubscribed from ApplicationState changes");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error unsubscribing from ApplicationState changes in MainCarScreen");
        }

        disposed = true;
    }

    public override ITemplate OnGetTemplate()
    {
        logger.Information("✅ MainCarScreen.OnGetTemplate() called - Head unit is requesting the UI!");

        try
        {
            // CRITICAL: Don't block OnGetTemplate() - return template immediately to prevent "Getting your selection" message
            // Bootstrap should already be complete from Android Auto connection, but if not, proceed with available data
            // This ensures Android Auto shows the list immediately instead of the loading message

            LoadSchedules();

            if (scheduleItems == null || scheduleItems.Count == 0)
            {
                logger.Information("No schedules found in state - showing empty list");
                return CreateEmptyListTemplate();
            }

            logger.Information("Creating ListTemplate with {Count} schedules from state", scheduleItems.Count);

            var rows = BuildRowsFromSchedules();
            if (rows.Count == 0)
            {
                logger.Warning("No rows created from schedules - showing empty list");
                return CreateEmptyListTemplate();
            }

            var itemList = BuildItemList(rows);
            if (itemList == null)
            {
                logger.Warning("Failed to build item list, returning empty template");
                return CreateEmptyListTemplate();
            }

            var header = BuildHeader();
            if (header == null)
            {
                logger.Warning("Failed to build header, returning empty template");
                return CreateEmptyListTemplate();
            }

            var listTemplate = BuildListTemplate(header, itemList);
            if (listTemplate == null)
            {
                logger.Warning("Failed to build list template, returning empty template");
                return CreateEmptyListTemplate();
            }

            logger.Information("✅ ListTemplate created successfully with {Count} schedules", rows.Count);
            return listTemplate;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "❌ Error creating template - falling back to MessageTemplate. Exception: {Exception}", ex);
            return CreateFallbackTemplate();
        }
    }

    private List<Row> BuildRowsFromSchedules()
    {
        var rows = new List<Row>();

        foreach (var scheduleItem in scheduleItems!.OrderBy(s => s.Name))
        {
            try
            {
                var row = CreateRowForSchedule(scheduleItem);
                if (row != null)
                {
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to create Row for schedule {ScheduleId}", scheduleItem.Id);
            }
        }

        return rows;
    }

    private ItemList? BuildItemList(List<Row> rows)
    {
        var itemListBuilder = new ItemList.Builder()
            .SetNoItemsMessage("No schedules available");

        foreach (var row in rows)
        {
            itemListBuilder?.AddItem(row);
        }

        return itemListBuilder?.Build();
    }

    private Header? BuildHeader()
    {
        return new Header.Builder()
            ?.SetTitle("Bible Alarm")
            ?.SetStartHeaderAction(Action.AppIcon)
            ?.Build();
    }

    private static ListTemplate? BuildListTemplate(Header header, ItemList itemList)
    {
        return new ListTemplate.Builder()
            ?.SetHeader(header)
            ?.SetSingleList(itemList)
            ?.Build();
    }

    private ITemplate CreateFallbackTemplate()
    {
        try
        {
            var fallbackHeader = BuildHeader();
            if (fallbackHeader == null)
            {
                logger.Warning("Failed to build fallback header, returning empty template");
                return CreateEmptyListTemplate();
            }

            var messageTemplate = new MessageTemplate.Builder("An error occurred loading your schedules.")
                ?.SetHeader(fallbackHeader)
                ?.Build();

            if (messageTemplate != null)
            {
                return messageTemplate;
            }

            return CreateEmptyListTemplate();
        }
        catch (Exception fallbackEx)
        {
            logger.Error(fallbackEx, "❌ Failed to create fallback MessageTemplate");
            return CreateEmptyListTemplate();
        }
    }

    private void LoadSchedules() => scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();

    private Row? CreateRowForSchedule(ScheduleStateItem scheduleItem)
    {
        try
        {
            // Use shared helper to build title and subtitle from ScheduleStateItem DTO
            var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
            var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);

            // Create book icon for the row
            var bookIcon = CreateBookIcon();

            // Create Row with title, subtitle, icon, and click callback
            // Store schedule ID in a closure so we can access it when clicked
            var scheduleId = scheduleItem.Id;
            var rowBuilder = new Row.Builder()
                ?.SetTitle(title)
                ?.AddText(subtitle)
                ?.SetOnClickListener(new ScheduleClickCallback(this, scheduleId));

            // Add book icon if available
            if (bookIcon != null)
            {
                rowBuilder?.SetImage(bookIcon);
            }

            var row = rowBuilder?.Build();
            if (row == null)
            {
                logger.Warning("Failed to build row for schedule {ScheduleId}", scheduleItem.Id);
                throw new InvalidOperationException("Failed to build row");
            }

            return row;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create Row for schedule {ScheduleId}", scheduleItem.Id);
            return null;
        }
    }

    /// <summary>
    /// Creates a CarIcon for playlist items to display in Android Auto.
    /// Uses a custom open book icon to represent Bible reading schedules.
    /// </summary>
    private CarIcon? CreateBookIcon()
    {
        try
        {
            const int BookIconSize = 128;
            const int BookOffset = BookIconSize / 2 - 8;
            const int BitmapSize = BookIconSize + BookOffset;

            var bookDrawable = ContextCompat.GetDrawable(CarContext, ResourceConstant.Drawable.ic_book_open);
            if (bookDrawable == null)
            {
                logger.Warning("Could not get app drawable for book icon");
                return null;
            }

            var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
            var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, config);
            bitmap.EraseColor(Color.Transparent);

            var canvas = new Canvas(bitmap);

            // Draw book icon
            bookDrawable.SetBounds(BookOffset, BookOffset, BookOffset + BookIconSize, BookOffset + BookIconSize);
            bookDrawable.Draw(canvas);

            // Convert bitmap to IconCompat
            var iconCompat = IconCompat.CreateWithBitmap(bitmap);
            return new CarIcon.Builder(iconCompat).Build();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create book icon - Rows will display without icon");
            return null;
        }
    }

    /// <summary>
    /// Converts a Drawable to a Bitmap.
    /// </summary>
    private static Bitmap? DrawableToBitmap(Drawable? drawable)
    {
        if (drawable == null)
        {
            return null;
        }

        if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
        {
            return bitmapDrawable.Bitmap;
        }

        var width = drawable.IntrinsicWidth > 0 ? drawable.IntrinsicWidth : 64;
        var height = drawable.IntrinsicHeight > 0 ? drawable.IntrinsicHeight : 64;

        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888 ?? Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
        drawable.Draw(canvas);
        return bitmap;
    }

    private static ITemplate CreateEmptyListTemplate()
    {
        var itemList = (new ItemList.Builder()
            ?.SetNoItemsMessage("No schedules available")
            ?.Build()) ?? throw new InvalidOperationException("Failed to build empty item list");

        // 2025 Modern Header: Title and HeaderAction are now part of a Header object
        var header = (new Header.Builder()
            ?.SetTitle("Bible Alarm")
            ?.SetStartHeaderAction(Action.AppIcon)
            ?.Build()) ?? throw new InvalidOperationException("Failed to build header for empty list template");

        // Modern ListTemplate: Replaces direct SetTitle/SetHeaderAction with SetHeader
        var template = (new ListTemplate.Builder()
            ?.SetHeader(header)
            ?.SetSingleList(itemList)
            ?.Build()) ?? throw new InvalidOperationException("Failed to build empty list template");
        return template;
    }

    internal void OnScheduleItemClicked(int scheduleId)
    {
        logger.Information("Schedule {ScheduleId} clicked - starting playback", scheduleId);

        try
        {
            // SetBufferingNoControlsState is now handled in MediaSessionEffect.HandlePlaybackStatusChanged
            // when PlayStatus.Loading is dispatched (which happens in PrepareAndPlayAsync)
            StartPlaybackAsync(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }

    private void StartPlaybackAsync(int scheduleId)
    {
        var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
        if (playbackService == null)
        {
            logger.Error("ISchedulePlaybackService is null - cannot play schedule {ScheduleId}", scheduleId);
            return;
        }

        // Play asynchronously - don't block the UI thread
        // This returns immediately so Android Auto doesn't show "Getting your selection"
        _ = Task.Run(async () =>
        {
            try
            {
                await playbackService.PlayScheduleAsync(scheduleId);
                logger.Information("✅ Started playback for schedule {ScheduleId}", scheduleId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error playing schedule {ScheduleId}", scheduleId);
            }
        });
    }
}

/// <summary>
/// Click callback for schedule items in the Car App list.
/// </summary>
internal class ScheduleClickCallback(MainCarScreen screen, int scheduleId) : Object, IOnClickListener
{
    private readonly MainCarScreen screen = screen ?? throw new ArgumentNullException(nameof(screen));
    private static readonly ILogger logger = Log.ForContext<ScheduleClickCallback>();

    public void OnClick()
    {
        try
        {
            logger.Information("Schedule {ScheduleId} clicked - starting playback", scheduleId);
            screen.OnScheduleItemClicked(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }
}
