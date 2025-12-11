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
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
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
        
        Logger.Information("CarAppService.OnCreate() called - Initializing bootstrap (background service)");
        
        // Initialize bootstrap asynchronously to avoid blocking UI thread
        // Android Auto services can be created during app startup, so we must not block
        _ = Task.Run(async () =>
        {
            try
            {
                MauiAppHolder.CreateAndStore();
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                
                // Wait for bootstrap to complete before proceeding
                // This ensures database migrations are finished before accessing schedule database
                await MauiProgram.WaitForBootstrapAsync();
                
                Logger.Information("✅ CarAppService.OnCreate() completed - Bootstrap initialized and ready");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing bootstrap in CarAppService");
            }
        });
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
public class MainCarScreen : AndroidX.Car.App.Screen
{
    private static readonly ILogger Logger = Log.ForContext<MainCarScreen>();
    private readonly MediaSessionManager _mediaSessionManager;
    private List<AlarmSchedule>? _schedules;
    private Dictionary<string, Bible.Alarm.Shared.Models.Media.Language>? _languagesDict;

    public MainCarScreen(CarContext carContext, MediaSessionManager mediaSessionManager) : base(carContext)
    {
        _mediaSessionManager = mediaSessionManager ?? throw new ArgumentNullException(nameof(mediaSessionManager));
        Logger.Information("✅ MainCarScreen created");
    }

    public override ITemplate OnGetTemplate()
    {
        Logger.Information("✅ MainCarScreen.OnGetTemplate() called - Head unit is requesting the UI!");
        
        try
        {
            // Ensure bootstrap is complete before accessing state
            // This is critical for Android Auto to show schedule list and handle media playback
            MauiProgram.WaitForBootstrap();
            Logger.Debug("Bootstrap completed, loading schedules from state");
            
            // Get the MediaSessionCompat to verify it's initialized
            var mediaSession = _mediaSessionManager.GetOrCreate();
            var sessionToken = mediaSession.SessionToken;
            
            if (sessionToken == null)
            {
                Logger.Warning("MediaSessionCompat.SessionToken is null - MediaSession may not be fully initialized yet");
            }
            else
            {
                Logger.Information("✅ MediaSession token available: {Token}", sessionToken.ToString());
            }
            
            // Load schedules from state (fast, no database access)
            // Schedules are already loaded during bootstrap
            LoadSchedules();
            
            if (_schedules == null || _schedules.Count == 0)
            {
                Logger.Information("No schedules found in state - showing empty list");
                return CreateEmptyListTemplate();
            }
            
            Logger.Information("Creating ListTemplate with {Count} schedules from state", _schedules.Count);
            
            // Create list of Row items for each schedule
            var rows = new List<Row>();
            
            foreach (var schedule in _schedules.OrderBy(s => s.Name))
            {
                try
                {
                    var row = CreateRowForSchedule(schedule);
                    if (row != null)
                    {
                        rows.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to create Row for schedule {ScheduleId}", schedule.Id);
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
        try
        {
            Logger.Debug("Loading schedules from state for CarAppService");
            
            // Get state from service provider - schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();
            
            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                // Convert ObservableHashSet to List for easier iteration
                _schedules = state.Value.Schedules.ToList();
                Logger.Information("Loaded {Count} schedules from state for CarAppService", _schedules.Count);
            }
            else
            {
                Logger.Warning("No schedules found in state - state may not be initialized yet");
                _schedules = new List<AlarmSchedule>();
            }
            
            // Pre-load all languages for efficient lookup
            // Use Task.Run to avoid blocking the main thread, but wait with timeout
            var bibleTranslationService = ServiceProviderManager.GetService<IBibleTranslationService>();
            if (bibleTranslationService != null)
            {
                try
                {
                    // Try to get languages with a short timeout to avoid blocking
                    var languageTask = bibleTranslationService.GetDistinctLanguagesAsync();
                    if (languageTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        _languagesDict = languageTask.Result;
                    }
                    else
                    {
                        Logger.Warning("Language loading timed out - continuing without language names");
                        _languagesDict = new Dictionary<string, Bible.Alarm.Shared.Models.Media.Language>();
                    }
                }
                catch (Exception langEx)
                {
                    Logger.Warning(langEx, "Error loading languages - continuing without language names");
                    _languagesDict = new Dictionary<string, Bible.Alarm.Shared.Models.Media.Language>();
                }
            }
            else
            {
                Logger.Warning("IBibleTranslationService is null - language lookup may fail");
                _languagesDict = new Dictionary<string, Bible.Alarm.Shared.Models.Media.Language>();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading schedules from state in MainCarScreen");
            _schedules = new List<AlarmSchedule>();
            _languagesDict = new Dictionary<string, Bible.Alarm.Shared.Models.Media.Language>();
        }
    }
    
    private Row? CreateRowForSchedule(AlarmSchedule schedule)
    {
        try
        {
            // Title: Schedule Name (if not empty), otherwise "Schedule {Id}"
            var title = !string.IsNullOrWhiteSpace(schedule.Name) 
                ? schedule.Name 
                : $"Schedule {schedule.Id}";
            
            // Build subtitle: Language, Book, Chapter (if BibleReadingSchedule exists)
            var subtitleParts = new List<string>();
            
            if (schedule.BibleReadingSchedule != null && _languagesDict != null)
            {
                var bibleReading = schedule.BibleReadingSchedule;
                
                // Get language name
                string? languageName = null;
                if (_languagesDict.TryGetValue(bibleReading.LanguageCode, out var language))
                {
                    languageName = language.Name;
                }
                else
                {
                    languageName = bibleReading.LanguageCode; // Fallback to code if name not found
                }
                
                // Build subtitle with Language, Book Number, Chapter Number
                // Use data directly from schedule to avoid any async calls that could block
                // This prevents blocking OnGetTemplate() which must return quickly (< 1 second)
                if (!string.IsNullOrWhiteSpace(languageName))
                {
                    subtitleParts.Add(languageName);
                }
                
                // Add book number (we skip book name lookup to avoid async calls)
                // Book number is sufficient for identification
                if (bibleReading.BookNumber > 0)
                {
                    subtitleParts.Add($"Book {bibleReading.BookNumber}");
                }
                
                // Add chapter number
                if (bibleReading.ChapterNumber > 0)
                {
                    subtitleParts.Add($"Chapter {bibleReading.ChapterNumber}");
                }
            }
            
            // Set subtitle - Language, Book, Chapter (or status/time if no Bible reading)
            string subtitle;
            if (subtitleParts.Count > 0)
            {
                subtitle = string.Join(" • ", subtitleParts);
            }
            else
            {
                // Fallback: show status and time if no Bible reading schedule
                var statusText = schedule.IsEnabled ? "Enabled" : "Disabled";
                var timeText = schedule.TimeText;
                subtitle = $"{statusText} • {timeText}";
            }
            
            // Create Row with title, subtitle, and click callback
            // Store schedule ID in a closure so we can access it when clicked
            var scheduleId = schedule.Id;
            var rowBuilder = new Row.Builder()
                .SetTitle(title)
                .AddText(subtitle)
                .SetOnClickListener(new ScheduleClickCallback(this, scheduleId));
            
            var row = rowBuilder.Build();
            
            return row;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to create Row for schedule {ScheduleId}", schedule.Id);
            return null;
        }
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
            // Get playback service and play the schedule
            var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
            if (playbackService != null)
            {
                // Play asynchronously - don't block the UI thread
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
