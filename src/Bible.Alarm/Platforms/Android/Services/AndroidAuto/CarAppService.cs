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
using Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;
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
    private readonly CarAppServiceInitializer initializer = new(logger);

    public override void OnCreate()
    {
        base.OnCreate();
        logger.Information("CarAppService.OnCreate() called - Ensuring MauiApp is created");

        initializer.InitializeMediaSession();
        initializer.InitializeBootstrapInBackground();
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
    private readonly CarScreenStateManager stateManager = new(logger);
    private readonly CarScreenTemplateBuilder templateBuilder = new(logger);
    private readonly CarScreenActionHandler actionHandler = new(logger);
    private readonly CarScreenPlaybackHandler playbackHandler = new(logger);
    private IState<ApplicationState>? applicationState;
    private AndroidAutoScheduleChangeTracker? scheduleChangeTracker;
    private bool disposed;

    public MainCarScreen(CarContext carContext, MediaSessionManager mediaSessionManager) : base(carContext)
    {
        this.mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        logger.Information("✅ MainCarScreen created");

        // Initialize state management
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
            // Load schedules using state manager
            stateManager.LoadScheduleItems();

            // Build template using template builder
            var refreshAction = actionHandler.CreateRefreshAction();
            return templateBuilder.BuildMainTemplate(stateManager.ScheduleItems, refreshAction);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "❌ Error creating template - falling back to MessageTemplate. Exception: {Exception}", ex);
            return CreateFallbackTemplate();
        }
    }

    private ITemplate CreateFallbackTemplate()
    {
        var message = new MessageTemplate.Builder("Error loading schedules")
            .SetTitle("Bible Alarm")
            .SetHeaderAction(Action.AppIcon)
            .Build();
        return message;
    }

    internal void OnScheduleItemClicked(int scheduleId)
    {
        playbackHandler.HandleScheduleItemClicked(scheduleId);
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
