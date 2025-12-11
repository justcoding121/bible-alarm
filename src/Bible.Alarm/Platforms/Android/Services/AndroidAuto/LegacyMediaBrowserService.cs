#nullable enable
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Media;
using AndroidX.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using System.Collections.Generic;
using System.Linq;

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
    private static readonly ILogger Logger = Log.ForContext<LegacyMediaBrowserService>();
    private MediaSessionCompat? _session;
    private MediaSessionManager? _mediaSessionManager;

    public override void OnCreate()
    {
        base.OnCreate();
        
        Logger.Information("LegacyMediaBrowserService.OnCreate() called - Ensuring MauiApp is created");
        
        // Ensure MauiApp is created and bootstrap is initialized (idempotent - safe to call multiple times)
        // Bootstrap initialization is thread-safe and will only run once even if called from multiple services
        _ = Task.Run(() =>
        {
            try
            {
                MauiAppHolder.CreateAndStore();
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                Logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Bootstrap initialization started");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing bootstrap in LegacyMediaBrowserService");
            }
        });
        
        // Initialize MediaSession synchronously (required for SessionToken)
        // This will work even if bootstrap is still running
        try
        {
            // Get MediaSessionManager from service provider (may be null if bootstrap not complete)
            _mediaSessionManager = ServiceProviderManager.GetService<MediaSessionManager>();
            if (_mediaSessionManager == null)
            {
                Logger.Warning("MediaSessionManager is null - bootstrap may not be complete yet. Will retry when service is bound.");
                return;
            }
            
            _session = _mediaSessionManager.GetOrCreate(true);
            if (_session == null)
            {
                Logger.Error("MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken");
                return;
            }
            
            // Verify SessionToken is available before setting it
            if (_session.SessionToken == null)
            {
                Logger.Error("MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized");
                return;
            }
            
            // THIS IS THE KEY LINE — both systems now see the same session
            SessionToken = _session.SessionToken;
            
            Logger.Information("SessionToken successfully set: {Token}", SessionToken?.ToString() ?? "null");
            Logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Legacy Android Auto is connecting! SessionToken set correctly.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing MediaSession in LegacyMediaBrowserService - will retry when service is bound");
        }
    }

    public override MediaBrowserServiceCompat.BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        Logger.Information("✅ OnGetRoot called for client: {ClientPackageName} (UID: {ClientUid})", 
            clientPackageName, clientUid);
        
        // Standard media root ID for Android Auto/AAOS compatibility
        // The root ID "__ID_ROOT__" is a common practice for media apps
        // Android Auto and AAOS hosts are trusted by default when connecting to MediaBrowserService
        return new MediaBrowserServiceCompat.BrowserRoot("__ID_ROOT__", null);
    }

    public override void OnLoadChildren(string parentId, MediaBrowserServiceCompat.Result result)
    {
        Logger.Information("✅ OnLoadChildren called for parent: {ParentId}", parentId);
        
        try
        {
            // CRITICAL: Detach the result before starting async work
            // Android requires that OnLoadChildren either calls SendResult() synchronously
            // or calls Detach() before returning if the result will be sent asynchronously
            result.Detach();
            Logger.Debug("Result detached successfully for parent: {ParentId}", parentId);
            
            // Ensure bootstrap is complete before accessing state
            // This is critical for loading schedules to show in Android Auto
            // Since OnLoadChildren is synchronous, we use WaitForBootstrap() synchronously
            MauiProgram.WaitForBootstrap();
            Logger.Debug("Bootstrap completed, loading schedules from state for parent: {ParentId}", parentId);
            
            // Run work to load schedules and create MediaItems on background thread
            _ = Task.Run(() =>
            {
                try
                {
                    Logger.Debug("Starting schedule loading for parent: {ParentId}", parentId);
                    
                    // Load schedule state items from state using shared helper
                    var scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
                    
                    if (scheduleItems.Count == 0)
                    {
                        Logger.Warning("No schedules found in state for parent: {ParentId} - state may not be initialized yet", parentId);
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
                            
                            var mediaDescription = descriptionBuilder.Build();
                            
                            // Create MediaBrowserCompat.MediaItem with FLAG_PLAYABLE flag
                            // This indicates the item can be played when selected
                            // Note: MediaBrowserCompat is in Android.Support.V4.Media namespace
                            var mediaItem = new MediaBrowserCompat.MediaItem(
                                mediaDescription,
                                MediaBrowserCompat.MediaItem.FlagPlayable);
                            
                            mediaItems.Add(mediaItem);
                            
                            Logger.Debug("Added MediaItem for schedule: {ScheduleId} - Title: {Title}, Subtitle: {Subtitle}", 
                                scheduleItem.Id, title, mediaDescription.Subtitle);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Failed to create MediaItem for schedule {ScheduleId}", scheduleItem.Id);
                        }
                    }
                    
                    Logger.Information("Created {Count} MediaItems for Android Auto", mediaItems.Size());
                    result.SendResult(mediaItems);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error loading children in LegacyMediaBrowserService for parent: {ParentId}. Bootstrap may not have completed or services may not be available.", parentId);
                    try
                    {
                        // Always send a result, even if empty, to satisfy Android's requirement
                        // This prevents the IllegalStateException from occurring
                        result.SendResult(new Java.Util.ArrayList());
                        Logger.Debug("Sent empty result after error for parent: {ParentId}", parentId);
                    }
                    catch (Exception sendEx)
                    {
                        Logger.Error(sendEx, "Failed to send empty result after error for parent: {ParentId}. This may cause Android Auto connection issues.", parentId);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // If Detach() itself fails, try to send empty result synchronously
            Logger.Error(ex, "Critical error in OnLoadChildren before detaching result for parent: {ParentId}", parentId);
            try
            {
                result.SendResult(new Java.Util.ArrayList());
            }
            catch (Exception sendEx)
            {
                Logger.Error(sendEx, "Failed to send result synchronously after critical error for parent: {ParentId}", parentId);
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
        Logger.Information("✅ LegacyMediaBrowserService.OnBind() called with intent: {Action}", 
            intent?.Action);
        
        // Log the intent details to help debug binder conflicts
        if (intent != null)
        {
            Logger.Information("Intent component: {Component}, Package: {Package}, Categories: {Categories}", 
                intent.Component?.ClassName, intent.Package, string.Join(", ", intent.Categories ?? Array.Empty<string>()));
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
                        Logger.Information("SessionToken set in OnBind(): {Token}", SessionToken?.ToString() ?? "null");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not set SessionToken in OnBind() - bootstrap may still be running");
            }
        }
        
        return base.OnBind(intent);
    }

    public override void OnDestroy()
    {
        Logger.Information("✅ LegacyMediaBrowserService destroyed - Releasing MediaSession");
        
        // Release the shared MediaSessionCompat to clean up resources
        // This ensures proper cleanup when the service is stopped by the system
        _session?.Release();
        _session = null;
        
        base.OnDestroy();
    }
}

