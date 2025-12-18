#nullable enable
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Validation;
using Bible.Alarm.Common;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
[IntentFilter(new[] { "androidx.car.app.CarAppService" })]
// Links the CarAppService to the description file for modern Android Auto/AAOS discovery
[MetaData("androidx.car.app.host.description", Resource = "@xml/car_app_desc")]
// Declare minimum Car App Library API level (using integer resource)
[MetaData("androidx.car.app.minCarApiLevel", Resource = "@integer/car_app_min_api_level")]
// Declare target Car App Library API level (using integer resource)
[MetaData("androidx.car.app.targetCarApiLevel", Resource = "@integer/car_app_target_api_level")]
[Register("bible.alarm.platforms.android.services.androidauto.CarAppService")]
public class CarAppService : AndroidX.Car.App.CarAppService
{
    private static readonly ILogger Logger = Log.ForContext<CarAppService>();

    public override void OnCreate()
    {
        base.OnCreate();
        
        Logger.Information("CarAppService.OnCreate() called - Ensuring MauiApp is created");

        // Create the DI container immediately (fast) so ServiceProviderManager is available synchronously.
        // Then publish a blank, non-interactive loading UI to Android Auto ASAP.
        try
        {
            MauiAppHolder.CreateAndStore();
            var mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
            mediaSessionManager?.GetOrCreate(true);
            mediaSessionManager?.SetBlankLoadingState();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "CarAppService.OnCreate: failed to create MauiApp / initialize MediaSession loading state");
        }
        
        // Ensure MauiApp is created and bootstrap is initialized (idempotent - safe to call multiple times)
        // Bootstrap initialization is thread-safe and will only run once even if called from multiple services
        _ = Task.Run(async () =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                Logger.Information("✅ CarAppService.OnCreate() completed - Bootstrap initialization started");
                
                // Wait for bootstrap to complete and set initial metadata
                // Use a longer timeout for OnCreate since it's not blocking the UI
                try
                {
                    await MauiProgram.WaitForBootstrapAsync(timeoutMs: 30000);
                    await SetInitialScheduleMetadataAsync();
                }
                catch (Exception bootstrapEx)
                {
                    Logger.Warning(bootstrapEx, "Bootstrap timed out in CarAppService.OnCreate - will retry when template is requested");
                    // Don't throw - allow service to continue, template will be generated when bootstrap completes
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing bootstrap in CarAppService");
            }
        });
    }
    
    private async Task SetInitialScheduleMetadataAsync()
    {
        try
        {
            Logger.Debug("SetInitialScheduleMetadataAsync: Setting metadata to first schedule after bootstrap");
            
            var defaultScheduleService = ServiceProviderManager.GetService<IDefaultScheduleService>();
            if (defaultScheduleService == null)
            {
                Logger.Warning("SetInitialScheduleMetadataAsync: IDefaultScheduleService not available");
                return;
            }
            
            var metadata = await defaultScheduleService.GetNextScheduleTrackMetaDataAsync();
            
            // Update metadata on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
                if (mediaSessionManager == null)
                {
                    Logger.Warning("SetInitialScheduleMetadataAsync: MediaSessionManager is null");
                    return;
                }
                
                // Set metadata using MediaSessionManager
                mediaSessionManager.UpdateMetadata(
                    metadata.Title,
                    metadata.Artist,
                    metadata.Album,
                    metadata.ScheduleId);

                // Return to stopped state (idle) with normal actions once metadata is ready.
                mediaSessionManager.UpdatePlaybackStateForStop();
                
                Logger.Information("SetInitialScheduleMetadataAsync: Set metadata to first schedule - ScheduleId={ScheduleId}, Title={Title}, Artist={Artist}",
                    metadata.ScheduleId, metadata.Title, metadata.Artist);
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error setting initial schedule metadata in CarAppService");
        }
    }

    public override HostValidator CreateHostValidator()
    {
        Logger.Information("✅ CarAppService.CreateHostValidator() called");
        return HostValidator.AllowAllHostsValidator; // TODO: Replace with proper host validation in production
    }

    public override Session OnCreateSession()
    {
        try
        {
            Logger.Information("✅ CarAppService.OnCreateSession() called - Modern Android Auto is connecting!");
            // Get ModernMediaSession from service provider
            var session = ServiceProviderManager.GetService<ModernMediaSession>();
            if (session == null)
            {
                Logger.Error("ModernMediaSession is null - cannot create session");
                throw new InvalidOperationException("ModernMediaSession is null");
            }
            Logger.Information("✅ CarAppService.OnCreateSession() completed successfully");
            return session;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ CarAppService.OnCreateSession() failed - This may indicate a Binder interface mismatch");
            throw;
        }
    }
}

/// <summary>
/// Session for modern Android Auto that attaches the shared MediaSessionCompat.
/// </summary>
public class ModernMediaSession : Session
{
    private static readonly ILogger Logger = Log.ForContext<ModernMediaSession>();
    private readonly MediaSessionCompat _phoneSession;
    private readonly MediaSessionManager _mediaSessionManager;

    public ModernMediaSession(MediaSessionManager mediaSessionManager)
    {
        _mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        _phoneSession = mediaSessionManager.GetOrCreate(true);
        Logger.Information("✅ ModernMediaSession created");
    }

    public override AndroidX.Car.App.Screen OnCreateScreen(Intent intent)
    {
        Logger.Information("✅ ModernMediaSession.OnCreateScreen() called with intent: {Action}", intent?.Action);
        
        // The media session is attached via the template in MainCarScreen
        Logger.Information("✅ Modern Android Auto connected — will attach shared MediaSession via template");
        
        return new MainCarScreen(CarContext, _mediaSessionManager);
    }
}

/// <summary>
/// Main car screen that displays the schedule list with playback controls.
/// </summary>
public class MainCarScreen : AndroidX.Car.App.Screen, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<MainCarScreen>();
    private readonly MediaSessionManager _mediaSessionManager;
    private List<ScheduleStateItem>? _scheduleItems;
    private IState<ApplicationState>? _applicationState;
    private AndroidAutoScheduleChangeTracker? _scheduleChangeTracker;
    private bool _disposed;

    public MainCarScreen(CarContext carContext, MediaSessionManager mediaSessionManager) : base(carContext)
    {
        _mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        Logger.Information("✅ MainCarScreen created");
        
        // Subscribe to state changes to refresh the template when schedules are added/updated/removed
        try
        {
            _applicationState = ServiceProviderManager.GetService<IState<ApplicationState>>();
            if (_applicationState != null)
            {
                // Initialize schedule change tracker
                _scheduleChangeTracker = new AndroidAutoScheduleChangeTracker();
                _scheduleChangeTracker.Initialize(_applicationState);
                
                _applicationState.StateChanged += OnApplicationStateChanged;
                Logger.Information("✅ MainCarScreen subscribed to schedule list changes");
            }
            else
            {
                Logger.Warning("IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error subscribing to ApplicationState changes in MainCarScreen");
        }
    }
    
    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (_scheduleChangeTracker == null)
                return;
            
            // Check if schedules changed using the shared tracker
            if (_scheduleChangeTracker.CheckForChanges())
            {
                Logger.Debug("Schedule list changed - invalidating template to refresh Android Auto UI");
                
                // Invalidate the template to force Android Auto to call OnGetTemplate() again
                Invalidate();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error checking schedule list changes");
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        try
        {
            if (_applicationState != null)
            {
                _applicationState.StateChanged -= OnApplicationStateChanged;
                Logger.Debug("MainCarScreen unsubscribed from ApplicationState changes");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error unsubscribing from ApplicationState changes in MainCarScreen");
        }
        
        _disposed = true;
    }

    public override ITemplate OnGetTemplate()
    {
        Logger.Information("✅ MainCarScreen.OnGetTemplate() called - Head unit is requesting the UI!");
        
        try
        {
            // CRITICAL: Don't block OnGetTemplate() - return template immediately to prevent "Getting your selection" message
            // Bootstrap should already be complete from Android Auto connection, but if not, proceed with available data
            // This ensures Android Auto shows the list immediately instead of the loading message
            
            // Load schedules from state (fast, no database access)
            // Schedules are already loaded during bootstrap
            LoadSchedules();
            
            if (_scheduleItems == null || _scheduleItems.Count == 0)
            {
                Logger.Information("No schedules found in state - showing empty list");
                return CreateEmptyListTemplate();
            }
            
            Logger.Information("Creating ListTemplate with {Count} schedules from state", _scheduleItems.Count);
            
            // Create list of Row items for each schedule
            var rows = new List<Row>();
            
            foreach (var scheduleItem in _scheduleItems.OrderBy(s => s.Name))
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
                    Logger.Warning(ex, "Failed to create Row for schedule {ScheduleId}", scheduleItem.Id);
                }
            }
            
            if (rows.Count == 0)
            {
                Logger.Warning("No rows created from schedules - showing empty list");
                return CreateEmptyListTemplate();
            }
            
            // Create ItemList with all rows
            // Each row has its own OnClickListener set in CreateRowForSchedule
            var itemListBuilder = new ItemList.Builder()
                .SetNoItemsMessage("No schedules available");
            
            foreach (var row in rows)
            {
                itemListBuilder.AddItem(row);
            }
            
            var itemList = itemListBuilder.Build();
            
            // Create ListTemplate with the item list
            var listTemplate = new ListTemplate.Builder()
                .SetTitle("Bible Alarm")
                .SetHeaderAction(AndroidX.Car.App.Model.Action.AppIcon)
                .SetSingleList(itemList)
                .Build();
            
            Logger.Information("✅ ListTemplate created successfully with {Count} schedules", rows.Count);
            return listTemplate;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ Error creating template - falling back to MessageTemplate. Exception: {Exception}", ex);
            // Fallback to message template on error
            try
            {
                return new MessageTemplate.Builder("Bible Alarm")
                    .SetTitle("Bible Alarm")
                    .SetHeaderAction(AndroidX.Car.App.Model.Action.AppIcon)
                    .Build();
            }
            catch (Exception fallbackEx)
            {
                Logger.Error(fallbackEx, "❌ Failed to create fallback MessageTemplate");
                // Last resort - return null and let Android Auto handle it
                return null;
            }
        }
    }
    
    private void LoadSchedules()
    {
        _scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
    }
    
    private Row? CreateRowForSchedule(ScheduleStateItem scheduleItem)
    {
        try
        {
            // Use shared helper to build title and subtitle from ScheduleStateItem DTO
            var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
            var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);
            
            // Create headphone icon for the row
            var headphoneIcon = CreateHeadphoneIcon();
            
            // Create Row with title, subtitle, icon, and click callback
            // Store schedule ID in a closure so we can access it when clicked
            var scheduleId = scheduleItem.Id;
            var rowBuilder = new Row.Builder()
                .SetTitle(title)
                .AddText(subtitle)
                .SetOnClickListener(new ScheduleClickCallback(this, scheduleId));
            
            // Add headphone icon if available
            if (headphoneIcon != null)
            {
                rowBuilder.SetImage(headphoneIcon);
            }
            
            var row = rowBuilder.Build();
            
            return row;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to create Row for schedule {ScheduleId}", scheduleItem.Id);
            return null;
        }
    }

    /// <summary>
    /// Creates a CarIcon for headphone/audio playback.
    /// Uses Android's standard media play icon to represent audio/media content.
    /// Note: Currently returns null as IconCompat requires AndroidX.Core package reference.
    /// Rows will display without icons until the proper package is added.
    /// TODO: Add AndroidX.Core.Graphics.Drawables package and use IconCompat.CreateWithResource()
    /// </summary>
    private CarIcon? CreateHeadphoneIcon()
    {
        // TODO: Implement icon creation once AndroidX.Core.Graphics.Drawables package is available
        // Example implementation:
        // var iconCompat = IconCompat.CreateWithResource(CarContext, Android.Resource.Drawable.IcMediaPlay);
        // return new CarIcon.Builder(iconCompat).Build();
        
        Logger.Debug("Icon creation skipped - AndroidX.Core.Graphics.Drawables.IconCompat not available");
        return null;
    }
    
    private ITemplate CreateEmptyListTemplate()
    {
        var itemList = new ItemList.Builder()
            .SetNoItemsMessage("No schedules available")
            .Build();
        
        return new ListTemplate.Builder()
            .SetTitle("Bible Alarm")
            .SetHeaderAction(AndroidX.Car.App.Model.Action.AppIcon)
            .SetSingleList(itemList)
            .Build();
    }
    
    internal void OnScheduleItemClicked(int scheduleId)
    {
        Logger.Information("Schedule {ScheduleId} clicked - starting playback", scheduleId);
        
        try
        {
            // Immediately update MediaSession to a non-interactive Buffering state.
            // This prevents Android Auto from showing tappable controls / "Getting your selection" while we start playback.
            try
            {
                _mediaSessionManager.SetBufferingNoControlsState();
                Logger.Debug("Set MediaSession to buffering no-controls state immediately on click");
            }
            catch (Exception mediaEx)
            {
                Logger.Warning(mediaEx, "Failed to update MediaSession state on click - continuing anyway");
            }
            
            // Get playback service and play the schedule
            var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
            if (playbackService != null)
            {
                // Play asynchronously - don't block the UI thread
                // This returns immediately so Android Auto doesn't show "Getting your selection"
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await playbackService.PlayScheduleAsync(scheduleId);
                        Logger.Information("✅ Started playback for schedule {ScheduleId}", scheduleId);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error playing schedule {ScheduleId}", scheduleId);
                    }
                });
            }
            else
            {
                Logger.Error("ISchedulePlaybackService is null - cannot play schedule {ScheduleId}", scheduleId);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }
}

/// <summary>
/// Click callback for schedule items in the Car App list.
/// </summary>
internal class ScheduleClickCallback : Java.Lang.Object, IOnClickListener
{
    private readonly MainCarScreen _screen;
    private readonly int _scheduleId;
    private static readonly ILogger Logger = Log.ForContext<ScheduleClickCallback>();

    public ScheduleClickCallback(MainCarScreen screen, int scheduleId)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        _scheduleId = scheduleId;
    }

    public void OnClick()
    {
        try
        {
            Logger.Information("Schedule {ScheduleId} clicked - starting playback", _scheduleId);
            _screen.OnScheduleItemClicked(_scheduleId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", _scheduleId);
        }
    }
}
