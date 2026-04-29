namespace Bible.Alarm.Shared.Constants;

/// <summary>
/// Application constants containing sensitive information and configuration values.
/// This file centralizes all URLs, API endpoints, and other sensitive information
/// to make them easier to manage and secure.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// JW.org API endpoints for media content
    /// </summary>
    public static class ApiEndpoints
    {
        /// <summary>JW CDN mirror hostnames (<c>b.</c> vs <c>app.</c>).</summary>
        public const string JwCdnHostB = "b.jw-cdn.org";
        public const string JwCdnHostApp = "app.jw-cdn.org";

        /// <summary>HTTPS scheme + host for JW CDN mirrors (no trailing slash).</summary>
        public const string JwCdnOriginHttpsB = "https://b.jw-cdn.org";
        public const string JwCdnOriginHttpsApp = "https://app.jw-cdn.org";

        /// <summary>GETPUBMEDIALINKS path under JW CDN (<c>/apis/pub-media/GETPUBMEDIALINKS</c>).</summary>
        public const string PubMediaApisGetPubMedialinksPath = "/apis/pub-media/GETPUBMEDIALINKS";

        /// <summary>Mediator API path after origin (<c>/apis/mediator/v1</c>).</summary>
        public const string MediatorApisV1Path = "/apis/mediator/v1";

        /// <summary>Mediator REST path prefix for category JSON (<c>/categories/{lang}/{categoryKey}</c>).</summary>
        public const string MediatorApiCategoriesPathPrefix = "/categories";

        /// <summary>
        /// Primary JW.org index service base URL for GETPUBMEDIALINKS (flat video/music, drama track lookup).
        /// </summary>
        public const string JwOrgIndexServiceBaseUrl = JwCdnOriginHttpsB + PubMediaApisGetPubMedialinksPath;

        /// <summary>
        /// Redundant base URLs for GETPUBMEDIALINKS (app. and b. are equivalent). Use for retry when a fetch fails.
        /// </summary>
        public static readonly string[] JwOrgIndexServiceBaseUrls =
        {
            JwCdnOriginHttpsB + PubMediaApisGetPubMedialinksPath,
            JwCdnOriginHttpsApp + PubMediaApisGetPubMedialinksPath
        };

        /// <summary>
        /// Primary JW.org Mediator API base URL for category-based content (dramas, etc.).
        /// </summary>
        public const string JwOrgMediatorApiBaseUrl = JwCdnOriginHttpsApp + MediatorApisV1Path;

        /// <summary>
        /// Redundant base URLs for Mediator API (b. and app. are equivalent). Use for random pick or retry.
        /// </summary>
        public static readonly string[] JwOrgMediatorApiBaseUrls =
        {
            JwCdnOriginHttpsB + MediatorApisV1Path,
            JwCdnOriginHttpsApp + MediatorApisV1Path
        };

        /// <summary>
        /// JW.org public languages list JSON (<c>/en/languages</c>). Catalog seeding and sign-language detection.
        /// </summary>
        public const string JwOrgLanguagesListUrl = "https://www.jw.org/en/languages";

        /// <summary>
        /// Media index file name prefix for new format (v2+)
        /// Old format files don't have this prefix and will be preserved for backward compatibility
        /// </summary>
        public const string MediaIndexFileNamePrefix = "v2-";

        /// <summary>Standard language index filename under category folders (<c>languages.json</c>).</summary>
        public const string MediaIndexLanguagesFileName = "languages.json";

        /// <summary>Publication index filename (<c>publications.json</c>).</summary>
        public const string MediaIndexPublicationsFileName = "publications.json";

        /// <summary>Section index filename (<c>sections.json</c>).</summary>
        public const string MediaIndexSectionsFileName = "sections.json";

        /// <summary>Track list filename (<c>tracks.json</c>).</summary>
        public const string MediaIndexTracksFileName = "tracks.json";

        /// <summary>Melody disc metadata filename (<c>disc.json</c>).</summary>
        public const string MediaIndexMelodyDiscInfoFileName = "disc.json";

        /// <summary>Video episode index filename (<c>episodes.json</c>).</summary>
        public const string MediaIndexVideoEpisodesFileName = "episodes.json";

        /// <summary>Bible English language-discovery artifact (<c>language-discovery.json</c>) under media/Bible/E.</summary>
        public const string MediaIndexLanguageDiscoveryFileName = "language-discovery.json";

        /// <summary>Media index segment for scripture bundle (<c>Audio/Bible/...</c> on device).</summary>
        public const string MediaIndexFolderAudio = "Audio";

        /// <summary>Instrumental melody subtree (<c>Music/Melodies</c>).</summary>
        public const string MediaIndexFolderMelodies = "Melodies";

        /// <summary>Vocal song subtree (<c>Music/Vocals</c>).</summary>
        public const string MediaIndexFolderVocals = "Vocals";
    }

    /// <summary>
    /// Database configuration constants
    /// </summary>
    public static class Database
    {
        /// <summary>
        /// Schedule database filename
        /// </summary>
        public const string ScheduleDatabaseFileName = "schedule.db";

        /// <summary>Legacy Schedule SQLite filename superseded by <see cref="ScheduleDatabaseFileName"/>.</summary>
        public const string ScheduleDatabaseLegacyBibleAlarmFileName = "bibleAlarm.db";

        /// <summary>Legacy Schedule SQLite filename superseded by <see cref="ScheduleDatabaseFileName"/>.</summary>
        public const string ScheduleDatabaseLegacyBibleAlarm2FileName = "bibleAlarm2.db";

        /// <summary>
        /// Media index database filename
        /// </summary>
        public const string MediaIndexDatabaseFileName = "mediaIndex.db";

        /// <summary>Suffix inserted before the extension when renaming the prior media index DB during upgrade (<c>_old</c> → <c>mediaIndex_old.db</c>).</summary>
        public const string MediaIndexDatabaseRenamedSuffix = "_old";

        /// <summary>SQLite WAL sidecar filename suffix.</summary>
        public const string SqliteWalFileSuffix = "-wal";

        /// <summary>SQLite SHM sidecar filename suffix.</summary>
        public const string SqliteShmFileSuffix = "-shm";

        /// <summary>SQLite rollback journal sidecar filename suffix.</summary>
        public const string SqliteJournalFileSuffix = "-journal";

        /// <summary>Typical auxiliary files beside the main SQLite DB (WAL, SHM, classic journal).</summary>
        public static readonly string[] SqliteAuxiliaryFileSuffixes =
        [
            SqliteWalFileSuffix,
            SqliteShmFileSuffix,
            SqliteJournalFileSuffix,
        ];

        /// <summary>
        /// SQLite connection string format for schedule database
        /// </summary>
        public const string ScheduleDatabaseConnectionStringFormat = "Filename={0}";

        /// <summary>
        /// SQLite connection string format for media index database
        /// </summary>
        public const string MediaIndexDatabaseConnectionStringFormat = "Filename={0}";
    }

    /// <summary>
    /// File system path constants
    /// </summary>
    public static class FilePaths
    {
        /// <summary>
        /// Media cache root directory name
        /// </summary>
        public const string MediaCacheDirectoryName = "MediaCache";

        /// <summary>
        /// Logs directory name
        /// </summary>
        public const string LogsDirectoryName = "logs";

        /// <summary>Windows identity folder under local app data (<c>Bible.Alarm</c>); Serilog fallback and WinUI exe name for <c>Process.GetProcessesByName</c>.</summary>
        public const string WindowsAppDataFolderName = "Bible.Alarm";

        /// <summary>Cache subdirectory under <see cref="WindowsAppDataFolderName"/> for Serilog when WinRT storage is unavailable.</summary>
        public const string WindowsAppDataCacheFolderName = "Cache";

        /// <summary>Early bootstrap diagnostic log before Serilog (<c>bootstrap.txt</c>) under <see cref="LogsDirectoryName"/>.</summary>
        public const string BootstrapDiagnosticLogFileName = "bootstrap.txt";

        /// <summary>Legacy media index version stamp file in storage root (<c>version.dat</c>); Preferences hold the primary value.</summary>
        public const string MediaIndexVersionLegacyFileName = "version.dat";

        /// <summary>
        /// Log file name pattern (without extension - Serilog will add date and extension)
        /// </summary>
        public const string LogFileNamePattern = "bible-alarm-";

        /// <summary>
        /// Media index zip file name
        /// </summary>
        public const string MediaIndexZipFileName = "index.zip";

        /// <summary>Catalog index layout: folder between index root and category folders (<c>media</c>).</summary>
        public const string MediaIndexCatalogRootMediaSegment = "media";

        /// <summary>Cataloger media index output: subdirectory containing SQLite under index root (<c>db</c>).</summary>
        public const string MediaIndexCatalogOutputDbDirectoryName = "db";

        /// <summary>Cataloger: failed publication codes file next to index for <c>--retry-failed</c>.</summary>
        public const string CatalogerLastRunFailedListFileName = "last_run_failed.txt";

        /// <summary>Cataloger: default failed-list filename under temp when no path is passed.</summary>
        public const string CatalogerTempFailedListFallbackFileName = "bible_alarm_last_run_failed.txt";

        /// <summary>
        /// Temporary extraction directory name
        /// </summary>
        public const string TempExtractionDirectoryName = "tmp";

        /// <summary>
        /// Silent MP3 filename for Android dummy queue (ID3 e.g. title "Bible Alarm", artist "Preparing...").
        /// Change this when updating the file so bootstrap copies the new file on existing installs.
        /// </summary>
        public const string SilentMp3FileName = "silent_preparing_v2.mp3";

        /// <summary>Removed from storage on Android bootstrap (replaced by <see cref="SilentMp3FileName"/>).</summary>
        public const string SilentMp3LegacyFileName = "silent.mp3";

        /// <summary>Removed from storage on Android bootstrap (superseded by <see cref="SilentMp3FileName"/>).</summary>
        public const string SilentMp3LegacyPreparingFileName = "silent_preparing.mp3";
    }

    /// <summary>
    /// Font Awesome bundled filenames and MAUI font registration aliases (<c>ConfigureFonts</c>).
    /// </summary>
    public static class Fonts
    {
        /// <summary>Font Awesome Solid OTF under app Resources.</summary>
        public const string FontAwesomeSolidFontFileName = "fa_solid_900.otf";

        /// <summary>Font Awesome Regular OTF under app Resources.</summary>
        public const string FontAwesomeRegularFontFileName = "fa_regular_400.otf";

        /// <summary>Font Awesome Brands OTF under app Resources.</summary>
        public const string FontAwesomeBrandsFontFileName = "fa_brands_400.otf";

        /// <summary>Alias registered for Solid (<c>FontFamily</c>).</summary>
        public const string FontAwesomeSolidAlias = "FontAwesomeSolid";

        /// <summary>Alias registered for Regular.</summary>
        public const string FontAwesomeRegularAlias = "FontAwesomeRegular";

        /// <summary>Alias registered for Brands.</summary>
        public const string FontAwesomeBrandsAlias = "FontAwesomeBrands";
    }

    /// <summary>
    /// Application settings and configuration constants
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// Application name for logging
        /// </summary>
        public const string ApplicationName = "Bible-Alarm";

        /// <summary>Human-readable app title (lock screen, CarPlay, notifications, MAUI Android <c>Label</c>).</summary>
        public const string ApplicationDisplayName = "Bible Alarm";
    }

    /// <summary>
    /// Cache and download configuration constants
    /// </summary>
    public static class CacheSettings
    {
        /// <summary>
        /// Media index update check interval in days (weekly = 7 days)
        /// The cataloger runs weekly on Sundays, so we check weekly to match the update frequency
        /// </summary>
        // Weekly
        public const int MediaIndexUpdateCheckDays = 7;

        /// <summary>
        /// Download retry attempts
        /// </summary>
        public const int DownloadRetryAttempts = 3;

        /// <summary>
        /// File exists check retry attempts
        /// </summary>
        public const int FileExistsCheckRetryAttempts = 3;

        /// <summary>
        /// Download timeout in seconds per track (includes connection and transfer time)
        /// </summary>
        public const int DownloadTimeoutSeconds = 30;

        /// <summary>
        /// Log file retention limit in days
        /// </summary>
        public const int LogFileRetentionDays = 7;
    }

    /// <summary>
    /// Review request configuration constants
    /// Based on industry best practices for production apps
    /// </summary>
    public static class ReviewSettings
    {
        /// <summary>
        /// Minimum number of app opens before requesting review.
        /// </summary>
        public const int MinimumAppOpens = 7;

        /// <summary>
        /// Minimum number of dismissals before requesting review
        /// Increased from 6 to 10 to align with production app best practices
        /// </summary>
        public const int MinimumDismissals = 10;

        /// <summary>
        /// Minimum days since first dismissal before requesting review
        /// Ensures users have had meaningful engagement with the app
        /// </summary>
        public const int MinimumDaysSinceFirstDismissal = 7;

        /// <summary>
        /// Minimum days since app install/first launch before requesting review
        /// Prevents asking too early in the user journey
        /// </summary>
        public const int MinimumDaysSinceInstall = 7;

        /// <summary>
        /// Minimum days since first app open before requesting review.
        /// </summary>
        public const int MinimumDaysSinceFirstOpen = 7;

        /// <summary>
        /// Retry window in days when review flow is not finalized.
        /// </summary>
        public const int RetryAfterDaysWhenNotFinalized = 30;

        /// <summary>
        /// Prevent duplicate app-open increments during rapid start/resume transitions.
        /// </summary>
        public const int MinimumMinutesBetweenCountedAppOpens = 5;
    }

    /// <summary>
    /// General settings keys used throughout the application
    /// </summary>
    public static class GeneralSettingsKeys
    {
        /// <summary>
        /// Key for alarm seeded flag
        /// </summary>
        public const string AlarmSeeded = "AlarmSeeded";

        /// <summary>
        /// Key for last played schedule ID
        /// </summary>
        public const string LastPlayedScheduleId = "LastPlayedScheduleId";

        /// <summary>
        /// Key for review requested flag
        /// </summary>
        public const string ReviewRequested = "ReviewRequested";

        /// <summary>
        /// Key for dismiss count
        /// </summary>
        public const string DismissCount = "DismissCount";

        /// <summary>
        /// Key for first dismissal date (ISO 8601 format)
        /// </summary>
        public const string FirstDismissalDate = "FirstDismissalDate";

        /// <summary>
        /// Key for app install/first launch date (ISO 8601 format)
        /// </summary>
        public const string AppInstallDate = "AppInstallDate";

        /// <summary>
        /// Key for app open count used by review prompting.
        /// </summary>
        public const string ReviewAppOpenCount = "ReviewAppOpenCount";

        /// <summary>
        /// Key for first app open date (ISO 8601 format).
        /// </summary>
        public const string ReviewFirstOpenDate = "ReviewFirstOpenDate";

        /// <summary>
        /// Key for last app open that was counted for review logic.
        /// </summary>
        public const string ReviewLastCountedAppOpenAtUtc = "ReviewLastCountedAppOpenAtUtc";

        /// <summary>
        /// Key for review attempt count.
        /// </summary>
        public const string ReviewAttemptCount = "ReviewAttemptCount";

        /// <summary>
        /// Key for last review attempt timestamp (ISO 8601 format).
        /// </summary>
        public const string ReviewLastAttemptAtUtc = "ReviewLastAttemptAtUtc";

        /// <summary>
        /// Key for last timestamp when user met review eligibility.
        /// </summary>
        public const string ReviewLastEligibleAtUtc = "ReviewLastEligibleAtUtc";

        /// <summary>
        /// Key indicating review flow is completed/finalized and should no longer prompt.
        /// </summary>
        public const string ReviewCompletedOrFinalized = "ReviewCompletedOrFinalized";

        /// <summary>
        /// Key indicating legacy review settings were migrated to the new format.
        /// </summary>
        public const string ReviewStateMigrated = "ReviewStateMigrated";

        /// <summary>Preferences key for bundled media index freshness (pairs with legacy <c>version.dat</c>).</summary>
        public const string MediaIndexVersion = "MediaIndexVersion";

        /// <summary>
        /// Key for Android battery optimization exclusion prompt shown flag
        /// </summary>
        public const string AndroidBatteryOptimizationExclusionPromptShown = "AndroidBatteryOptimizationExclusionPromptShown";
    }

    /// <summary>
    /// Logging configuration constants
    /// </summary>
    public static class Logging
    {
        /// <summary>
        /// Console output template for logging
        /// </summary>
        public const string ConsoleOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

        /// <summary>
        /// File output template for logging
        /// </summary>
        public const string FileOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

        /// <summary>
        /// Debug environment identifier
        /// </summary>
        public const string DebugEnvironment = "DEBUG";

        /// <summary>Placeholder when assembly informational version cannot be resolved.</summary>
        public const string AssemblyVersionFallback = "AssemblyVersionNotFound";

        /// <summary>Fallback when an exception or native error message is unavailable.</summary>
        public const string UnknownErrorFallback = "Unknown error";

        /// <summary>Serilog message templates for alarm ring/prepare and related flows.</summary>
        public static class AlarmDiagnostics
        {
            public const string RingingAlarmFailed = "An error happened when ringing the alarm.";
            public const string CreatingAlarmRingTaskFailed = "An error happened when creating the task to ring the alarm.";
            public const string PlayingAlarmFailed = "An error happened when playing alarm.";
            public const string StartingUserInitiatedPlaybackFailed = "An error happened when starting user-initiated playback.";
            public const string ReviewRequestedFailed = "An error happened when review was requested.";
        }

        /// <summary>Serilog templates shared by platform bootstrap / global exception handlers.</summary>
        public static class ProcessDiagnosticsLog
        {
            public const string UnobservedTaskException = "Unobserved task exception.";
            public const string UnobservedTaskExceptionInSchedulerJob = "Unobserved task exception in SchedulerJob";
            public const string ErrorStoppingPlaybackBeforeAlarmForSchedule = "Error stopping current playback before handling alarm for schedule {ScheduleId}";
            public const string UnhandledExceptionIsTerminating = "Unhandled exception. IsTerminating: {IsTerminating}";
            public const string UnhandledNonExceptionObjectIsTerminating =
                "Unhandled exception (non-Exception object): {ExceptionObject}. IsTerminating: {IsTerminating}";
            public const string UnhandledExceptionInSchedulerJob = "Unhandled exception in SchedulerJob";

            /// <summary>Logged when an alarm fires while playback is active (handlers stop playback first).</summary>
            public const string AlarmTriggeredWhilePlaybackActiveStoppingForNewAlarm =
                "Alarm triggered for schedule {ScheduleId} while playback is active - stopping current playback to handle new alarm";

            /// <summary>iOS ObjC marshaling diagnostics (<c>AppDelegate</c>).</summary>
            public const string ManagedExceptionMarshalingToObjCMode = "Managed exception marshaling to ObjC (Mode={Mode})";

            public const string ObjCExceptionCaughtModeException = "ObjC exception caught (Mode={Mode}, Exception={Exception})";

            /// <summary>Android: alarm fires with tap notification disabled.</summary>
            public const string AlarmTriggeredTapDisabledForegroundNoSound =
                "Alarm triggered for schedule {ScheduleId} with tap disabled - using foreground service notification (no sound)";

            public const string FetchFailedDuringModalAppearing = "Fetch failed during modal appearing";

            /// <summary>Android manual alarm broadcast playback failure (<c>ShowNotificationAsync</c>).</summary>
            public const string ErrorPlayingAlarmManually = "Error happened when playing alarm manually.";
        }

        /// <summary>Serilog templates when schedule lookup by id fails.</summary>
        public static class ScheduleLookupDiagnosticsLog
        {
            public const string NotFoundStoppingForegroundService =
                "Schedule {ScheduleId} not found - stopping foreground service";

            public const string NotFoundForDeletion =
                "Schedule {ScheduleId} not found for deletion";

            public const string LoadExistingScheduleNotFoundInDatabase =
                "LoadExistingScheduleAsync: Schedule {ScheduleId} not found in database";

            public const string SetScheduleIdNotFoundInState =
                "SetScheduleId: Schedule {ScheduleId} not found in state";
        }

        /// <summary>Android exact-alarm / <c>SCHEDULE_EXACT_ALARM</c> scheduling failures.</summary>
        public static class AndroidExactAlarmSchedulingLog
        {
            public const string SecurityExceptionSchedulingAlarmForSchedule =
                "SecurityException when scheduling alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.";

            public const string SecurityExceptionUpdatingSchedule =
                "SecurityException when updating schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.";
        }

        /// <summary>Linked CTS disposal failures (dispose path).</summary>
        public static class DisposableLifetimeLog
        {
            public const string ErrorDuringCancellationTokenSourceDisposal =
                "Error during cancellation token source disposal";
        }

        /// <summary>Schedule enable / notification permission paths.</summary>
        public static class ScheduleEnableDiagnosticsLog
        {
            public const string CannotEnableNotificationDeniedTapToPlay =
                "Cannot enable schedule {ScheduleId} with NotificationEnabled=true - notification permission denied";

            public const string CannotEnableIosRemindersPermissionDenied =
                "Cannot enable schedule {ScheduleId} - notification permission denied. iOS requires notification permission for reminders.";

            public const string PermissionRequestTimeoutForSchedule =
                "Permission request timeout for schedule {ScheduleId}";
        }

        /// <summary>Notification permission modal / Android settings helpers.</summary>
        public static class NotificationPermissionDiagnosticsLog
        {
            public const string FailedToOpenAndroidAppSettings = "Failed to open Android app settings";

            public const string ErrorInOnModalDismissedCallback =
                "Error in onModalDismissed callback";

            public const string ErrorInDismissCommand =
                "Error in DismissCommand";

            public const string ErrorCheckingNotificationPermissionStatus =
                "Error checking notification permission status";

            public const string ErrorRefreshingCanShowSystemPromptIos =
                "Error refreshing CanShowSystemPrompt (iOS)";

            public const string ErrorInAsyncPermissionCheckFromTimer =
                "Error in async permission check from timer";

            public const string ErrorInScheduledAutoDismiss =
                "Error in scheduled auto-dismiss";

            public const string ErrorUpdatingHomePageButtonVisibilityAfterModalClose =
                "Error updating home page button visibility after modal close";

            public const string ErrorRequestingNotificationPermission =
                "Error requesting notification permission";

            public const string ErrorInPermissionCheckTask =
                "Error in permission check task";

            public const string PermissionPollingAlreadyRunningStoppingExistingTask =
                "Permission polling already running - stopping existing task";

            public const string PermissionRequestCompleted =
                "Permission request completed";

            public const string PermissionCheckGrantedCurrentNotificationEnabled =
                "Permission check: granted={Granted}, currentNotificationEnabled={Current}";

            public const string NotificationPermissionGrantedUpdatingToggleOnCurrentValue =
                "Notification permission granted - updating toggle to ON. Current value: {Current}";

            public const string PermissionGrantedNotificationEnabledAlreadyTrueStoppingTask =
                "Permission granted and NotificationEnabled already true - stopping task";

            public const string PermissionDeniedJustRequestedWaitingBeforeFlipToggle =
                "Permission denied but we just requested permission - waiting for user response before flipping toggle";

            public const string NotificationPermissionDeniedUpdatingToggleOffCurrentValue =
                "Notification permission denied - updating toggle to OFF. Current value: {Current}";

            public const string PermissionDeniedNotificationEnabledAlreadyFalseNoUpdateNeeded =
                "Permission denied but NotificationEnabled already false - no update needed";

            public const string PermissionCheckTaskCancelled =
                "Permission check task cancelled";

            public const string StoppedPermissionCheckTask =
                "Stopped permission check task";

            public const string SuccessfullyOpenedAndroidAppSettings =
                "Successfully opened Android app settings";

            public const string DismissCommandAlreadyDismissingSkippingDuplicate =
                "DismissCommand: Already dismissing, skipping duplicate call";

            public const string CheckingNotificationPermissionStatus =
                "Checking notification permission status...";

            public const string PermissionCheckCompletedGrantedWasGranted =
                "Permission check completed - Granted: {IsGranted} (was {WasGranted})";

            public const string StartingPermissionCheckTimerForModal =
                "Starting permission check timer for notification permission modal";

            public const string PermissionCheckTimerStartedSuccessfully =
                "Permission check timer started successfully";

            public const string PermissionCheckAsyncCompletedGrantedCanShowWasGranted =
                "Permission check (async) completed - Granted: {IsGranted}, CanShowPrompt: {CanShow} (was {WasGranted})";

            public const string PermissionGrantedDetectedByPollingSchedulingAutoDismiss =
                "Permission granted detected by polling - scheduling auto-dismiss";

            public const string NotificationPermissionGrantedEventReceived =
                "Notification permission granted event received";

            public const string PermissionGrantedAutoDismissingModalInOneSecond =
                "Permission granted - auto-dismissing modal in 1 second";

            public const string NotificationPermissionDeniedEventReceived =
                "Notification permission denied event received";
        }

        /// <summary>Battery optimization modal persistence checks.</summary>
        public static class BatteryOptimizationDiagnosticsLog
        {
            public const string ErrorMarkingBatteryOptimizationModalAsShown =
                "Error marking battery optimization modal as shown";

            public const string ErrorCheckingIfBatteryOptimizationModalShouldBeShown =
                "Error checking if battery optimization modal should be shown";
        }

        /// <summary>Periodic scheduler / alarm reconciliation (<c>SchedulerService</c>).</summary>
        public static class SchedulerDiagnosticsLog
        {
            public const string BootstrapNotCompletedWaitingForBootstrap =
                "Bootstrap not completed yet, waiting for bootstrap before running scheduler";

            public const string FailedToWaitForBootstrapProceedingAnyway =
                "Failed to wait for bootstrap completion, proceeding with scheduler anyway";

            public const string ErrorInsideCleanupTask =
                "An error happenned inside cleanup task.";

            public const string SchedulerRunSkippedPreviousRunStillInProgress =
                "Scheduler run skipped (previous run still in progress)";

            public const string FailedToProcessSchedulerTaskDbDirectory =
                "Failed to process scheduler task. Db directory: {CacheRoot}";

            public const string RescheduledNextOccurrenceForSchedule =
                "Rescheduled next occurrence for schedule {ScheduleId}";

            public const string ErrorReschedulingNextOccurrenceForSchedule =
                "Error rescheduling next occurrence for schedule {ScheduleId}";

            public const string ErrorDisposingSemaphoreMayAlreadyBeDisposed =
                "Error disposing semaphore, may already be disposed";
        }

        /// <summary>Default DB seed / Preferences metadata bootstrap.</summary>
        public static class DatabaseSeedDiagnosticsLog
        {
            public const string FailedToSaveSeededScheduleMetadataToPreferences =
                "Failed to save seeded schedule metadata to Preferences";

            public const string FailedToEnsurePreferencesHasMetadata =
                "Failed to ensure Preferences has metadata";

            public const string SeededDefaultAlarmSchedule =
                "Seeded default alarm schedule. ScheduleId={ScheduleId}, Name={Name}";

            public const string CannotSeedDefaultAlarmPublicationsNotYetAvailable =
                "Cannot seed default alarm schedule yet - Bible publications not available. This is normal during early bootstrap or when using test data. Schedule will be created when publications are loaded.";

            public const string SavedSeededScheduleMetadataToPreferencesForAa =
                "Saved seeded schedule metadata to Preferences for Android Auto. ScheduleId={ScheduleId}, Title={Title}";

            public const string SavedExistingScheduleMetadataToPreferencesForAa =
                "Saved existing schedule metadata to Preferences for Android Auto. ScheduleId={ScheduleId}, Title={Title}";
        }

        /// <summary>New/existing schedule VM load (<c>ScheduleInitializationService</c>).</summary>
        public static class ScheduleInitializationDiagnosticsLog
        {
            public const string InitializeNewScheduleMappedSampleMusicCodes =
                "InitializeNewScheduleAsync: Mapped sample schedule. MusicLanguageCode={LanguageCode}, MusicTrackCode={TrackCode}, MusicPublicationCode={PublicationCode}, MusicSectionCode={SectionCode}";

            public const string InitializeNewSchedulePopulatingDisplayNames =
                "InitializeNewScheduleAsync: Populating display names for new schedule";

            public const string InitializeNewScheduleDisplayNamesPopulated =
                "InitializeNewScheduleAsync: Display names populated. LanguageName: {LanguageName}, PublicationName: {PublicationName}, SectionName: {SectionName}, MusicPublicationName: {MusicPublicationName}, MusicSectionName: {MusicSectionName}";

            public const string LoadExistingScheduleAlarmScheduleServiceNotAvailable =
                "LoadExistingScheduleAsync: AlarmScheduleService not available";

            public const string LoadExistingScheduleLoadingFromDatabase =
                "LoadExistingScheduleAsync: Loading schedule {ScheduleId} from database";

            public const string LoadExistingSchedulePreferringNoLanguagePubLanguageFromState =
                "LoadExistingScheduleAsync: Preferring no-language pub display language from state for schedule {ScheduleId} (LanguageCode: {LanguageCode})";

            public const string LoadExistingScheduleLoadedWithDisplayNames =
                "LoadExistingScheduleAsync: Loaded schedule {ScheduleId} with display names";
        }

        /// <summary><c>MediaSessionHelper</c> early Android Auto state.</summary>
        public static class AndroidMediaSessionHelperDiagnosticsLog
        {
            public const string FailedToApplyAndroidAutoBufferingState =
                "Failed to apply Android Auto buffering state";

            public const string FailedToApplyLastPlayedMetadataToMediaSession =
                "Failed to apply last played metadata to MediaSession";

            public const string MediaSessionCompatCreatedSuccessfullyInitialBufferingActiveHasToken =
                "MediaSessionCompat created successfully via MediaSessionHelper. Initial state: Buffering, Active: True, SessionToken available: {HasToken}";

            public const string CreatingSharedMediaSessionCompatInstance2025Standard =
                "Creating shared MediaSessionCompat instance (2025 Standard)";

            public const string MediaSessionCompatSessionTokenNullAfterCreationShouldNotHappen =
                "MediaSessionCompat.SessionToken is null after creation - this should not happen";

            public const string NoLastPlayedMetadataInPreferencesAlreadyBlankLoading =
                "No last played metadata found in Preferences - already in blank loading state";

            public const string ApplyingLastPlayedMetadataToMediaSessionTitleArtistScheduleId =
                "Applying last played metadata to MediaSession - Title: {Title}, Artist: {Artist}, ScheduleId: {ScheduleId}";

            public const string SuccessfullyAppliedLastPlayedMetadataToMediaSession =
                "Successfully applied last played metadata to MediaSession";

            public const string LoadedArtworkBitmapFromArtworkUrl =
                "Loaded artwork bitmap from: {ArtworkUrl}";

            public const string CouldNotLoadArtworkFromArtworkUrlOmitting =
                "Could not load artwork from: {ArtworkUrl} (file not found or invalid format) — omitting artwork";
        }

        /// <summary>Schedule listing display names for music (<c>ScheduleDisplayNameMusicHelper</c>).</summary>
        public static class ScheduleDisplayNameDiagnosticsLog
        {
            public const string ErrorPopulatingMusicLanguageNameForMelody =
                "Error populating MusicLanguageName for melody";

            public const string ErrorPopulatingMusicLanguageName =
                "Error populating MusicLanguageName";

            public const string ErrorPopulatingMusicPublicationName =
                "Error populating MusicPublicationName";

            public const string ErrorPopulatingMusicSectionNameForPublicationSection =
                "Error populating MusicSectionName for publication {PublicationCode}, section {SectionCode}";

            public const string MusicSectionNameNotFoundForPublicationSection =
                "Music section name not found for publication {PublicationCode}, section {SectionCode}";

            public const string ErrorPopulatingMusicTrackName =
                "Error populating MusicTrackName";
        }

        /// <summary>Default schedule / rotation metadata (<c>DefaultScheduleService</c>).</summary>
        public static class DefaultScheduleServiceDiagnosticsLog
        {
            public const string GetNextScheduleTrackMetaDataFailedVerifyLastPlayedInDb =
                "GetNextScheduleTrackMetaDataAsync: Failed to verify last played schedule in DB, falling back to state";

            public const string FailedToSaveDefaultScheduleArtworkToFile =
                "Failed to save default schedule artwork to file";

            public const string FailedToCleanupOldDefaultScheduleArtworkFiles =
                "Failed to cleanup old default schedule artwork files";

            public const string GetNextScheduleUsingLastPlayedVerifiedInState =
                "GetNextScheduleTrackMetaDataAsync: Using last played schedule {ScheduleId} from preferences (verified in state)";

            public const string GetNextScheduleUsingLastPlayedVerifiedInDbStateNotLoaded =
                "GetNextScheduleTrackMetaDataAsync: Using last played schedule {ScheduleId} from preferences (verified in DB, state not yet loaded)";

            public const string GetNextScheduleLastPlayedNoLongerInDbQueryingFirst =
                "GetNextScheduleTrackMetaDataAsync: Last played schedule {ScheduleId} no longer exists in DB, querying for first schedule";

            public const string GetNextScheduleFoundFirstScheduleFromDatabase =
                "GetNextScheduleTrackMetaDataAsync: Found first schedule {ScheduleId} from database";

            public const string GetNextScheduleTrackMetaDataNoSchedulesInStateReturningFallback =
                "GetNextScheduleTrackMetaDataAsync: No schedules available in state; returning fallback metadata";

            public const string SavedDefaultScheduleMetadataToPreferences =
                "Saved default schedule metadata to Preferences - Title: {Title}, Artist: {Artist}, ScheduleId: {ScheduleId}";

            public const string GetNextScheduleInRotationNoSchedulesInStateReturningFallback =
                "GetNextScheduleInRotationMetadataAsync: No schedules in state; returning fallback";

            public const string GetNextScheduleInRotationNoNonMusicSchedulesReturningFallback =
                "GetNextScheduleInRotationMetadataAsync: No non-Music schedules; returning fallback";

            public const string GetNextScheduleInRotationRotatedToScheduleIndex =
                "GetNextScheduleInRotationMetadataAsync: Rotated to schedule index {Index}, ScheduleId={ScheduleId} (Music schedules skipped)";

            public const string GetTrackMetadataNoInternetReturningFallbackForSchedule =
                "GetTrackMetadataForScheduleAsync: No internet - returning fallback metadata for schedule {ScheduleId} without network call";

            public const string FailedToPrepareFirstTrackUsingFallbackMetadata =
                "Failed to prepare first track for schedule {ScheduleId}, using fallback metadata";

            public const string SavedDefaultScheduleArtworkToPathAndSize =
                "Saved default schedule artwork to {ArtworkPath}, size: {Size} bytes";

            public const string UsingListingFormatMetadataForSchedule =
                "Using listing-format metadata for schedule {ScheduleId}: Title={Title}, Artist={Artist}";

            public const string ReturningTrackMetadataForSchedule =
                "Returning track metadata for schedule {ScheduleId}: Title={Title}, Artist={Artist}, Album={Album}, HasArtwork={HasArtwork}";
        }

        /// <summary>Foreground service notifications (<c>ForegroundNotificationHelper</c>).</summary>
        public static class AndroidForegroundNotificationDiagnosticsLog
        {
            public const string ErrorCreatingNotificationChannelMayAlreadyExist =
                "Error creating notification channel (may already exist)";

            public const string CreateFallbackNotificationChannelCreationFailed =
                "CreateFallbackNotification: channel creation failed";

            public const string FailedToGetAppIconForAlarmNotification =
                "Failed to get app icon for alarm notification";
        }

        /// <summary>HTTP download retries and HEAD/GET probes (<c>DownloadService</c>).</summary>
        public static class DownloadDiagnosticsLog
        {
            public const string SkippingRetryNetworkConnectivityFailure =
                "Skipping retry for network connectivity failure: {Message}";

            public const string SkippingRetryPermanentHttpError =
                "Skipping retry for permanent HTTP error: {Message}";

            public const string RetryingDownloadAttemptAfterDelay =
                "Retrying download (attempt {RetryCount}/{MaxRetries}) after {DelaySeconds}s: {ExceptionMessage}";

            public const string DownloadCancelledForUrl =
                "Download cancelled for URL: {Url}";

            public const string FailedToDownloadPrimaryTryingAlternative =
                "Failed to download from primary URL: {Url}, trying alternative URL: {AlternativeUrl}";

            public const string FailedToDownloadAlternativeUrl =
                "Failed to download from alternative URL: {AlternativeUrl}";

            public const string FailedToDownloadPrimaryUrl =
                "Failed to download from primary URL: {Url}";

            public const string NoAlternativeUrlForFailedDownload =
                "No alternative URL provided for failed download: {Url}";

            public const string HeadRequestFailedTryingGetHeadersOnly =
                "HEAD request failed for URL: {Url}, trying GET with headers only";

            public const string FailedToGetContentLengthForUrl =
                "Failed to get Content-Length for URL: {Url}";

            public const string HeadRequestFailedFallingBackToGet =
                "HEAD request failed for URL: {Url}, falling back to GET request";
        }

        /// <summary>Schedule media cache (<c>MediaCacheService</c>).</summary>
        public static class MediaCacheDiagnosticsLog
        {
            public const string SkippingCacheSetupInvalidScheduleId =
                "Skipping cache setup for invalid schedule ID: {ScheduleId}";

            public const string ExceptionDownloadingMediaFilesForCaching =
                "An exception happened when downloading media files for caching.";

            public const string SkippingDownloadCachedFileExists =
                "Skipping download - cached file exists for lookup path: {LookUpPath}, URL: {Url}";

            public const string NoInternetSkippingDownloadForTrack =
                "No internet - skipping download for track: LookUpPath={LookUpPath}";

            public const string FailedToDownloadTrackContinuingNext =
                "Failed to download track: {Url} (lookup path: {LookUpPath}) for schedule {ScheduleId}. Continuing with next track.";

            public const string ExceptionDownloadingTrackContinuingNext =
                "Exception downloading track: {Url} (lookup path: {LookUpPath}) for schedule {ScheduleId}. Continuing with next track.";

            public const string InvalidScheduleIdInPlayItemMetadata =
                "Invalid schedule ID in PlayItem metadata: {ScheduleId}";

            public const string UsingCachedFileForTrack =
                "Using cached file for track: LookUpPath={LookUpPath}, URL={Url}, Path={CachedPath}";

            public const string TrackNotCachedNoInternetCannotPlay =
                "Track not cached and no internet. Cannot play track: LookUpPath={LookUpPath}, URL={Url}";

            public const string StreamingTrackFromCdnNotCached =
                "Streaming track from CDN (not cached): LookUpPath={LookUpPath}, URL={Url}";

            public const string NoInternetSkippingBackgroundCache =
                "No internet - skipping background cache for track: LookUpPath={LookUpPath}";

            public const string BackgroundCacheFailedForTrack =
                "Background cache failed for track: LookUpPath={LookUpPath}, URL={Url}";

            public const string CdnReturnedNotFoundRefetchingSectionPub =
                "CDN returned not found for track; refetching section/pub: LookUpPath={LookUpPath}";

            public const string RefetchDidNotYieldNewUrl =
                "Refetch did not yield a new URL; failing so UI can show error";
        }

        /// <summary>Bundled media index extraction, migration, and version persistence.</summary>
        public static class MediaIndexDiagnosticsLog
        {
            public const string FileOperationFailedRetryingLocked =
                "File operation failed (likely locked), retrying (attempt {RetryCount}/5) after {DelayMs}ms";

            public const string LockDisposedError =
                "MediaIndexService: @lock disposed error.";

            public const string OldMediaIndexDataCopyFailed =
                "Old media index data copy failed (partially or fully)";

            public const string ScheduleMediaBootstrapFetchFailed =
                "Schedule media bootstrap fetch failed (partially or fully)";

            public const string FailedToCleanupOrphanedSchedules =
                "Failed to cleanup orphaned schedules";

            public const string BackgroundCopyRemainingMediaDataFailed =
                "Background copy of remaining media data failed";

            public const string FailedToCleanupOldMediaIndexFiles =
                "Failed to clean up old media index files";

            public const string ClosedMediaDbContextConnectionAllowDeletion =
                "Closed MediaDbContext connection to allow database file deletion";

            public const string FailedToCloseMediaDbContextConnectionsGracefully =
                "Failed to close MediaDbContext connections gracefully, using ClearAllPools() as last resort";

            public const string FailedToReadVersionFromFile =
                "Failed to read version from {VersionFilePath}";

            public const string FailedToSaveVersionToLegacyFileNonCritical =
                "Failed to save version to {VersionFilePath} (non-critical, Preferences is primary)";

            public const string FailedToSaveVersionToPreferences =
                "Failed to save version to Preferences";

            public const string FailedToMigrateVersionToPreferencesNonCritical =
                "Failed to migrate version to Preferences (non-critical)";

            public const string SavedCurrentVersionToPreferences =
                "Saved current version {Version} to Preferences";

            public const string SavedCurrentVersionToVersionFileBackwardCompatibility =
                "Saved current version {Version} to {VersionFilePath} for backward compatibility";

            public const string MigratedVersionFromDatToPreferences =
                "Migrated version {Version} from version.dat to Preferences";
        }

        /// <summary>Single-track play, resume seek, and seek retry (<c>TrackPlaybackHandler</c>).</summary>
        public static class TrackPlaybackHandlerDiagnosticsLog
        {
            public const string CannotPlayTrackUriEmpty =
                "Cannot play track at index {TrackIndex}: URI is null or empty. URL: {TrackUrl}";

            public const string PlaybackStoppedDuringPrepareAborting =
                "Playback was stopped during PrepareAsync/WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync";

            public const string PlaybackStoppedDuringWaitAborting =
                "Playback was stopped during WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync";

            public const string ResumeSeekFromInMemoryFinishedDuration =
                "[Resume] Seek from in-memory FinishedDuration={Duration}, ScheduleId={ScheduleId}";

            public const string ResumeFallbackDbFinishedDuration =
                "[Resume] Fallback: in-memory FinishedDuration was zero but DB has {Duration} for ScheduleId={ScheduleId}. Using DB value.";

            public const string ResumeNoSeekBothZero =
                "[Resume] No seek: in-memory FinishedDuration=Zero, DB FinishedDuration=Zero, ScheduleId={ScheduleId}";

            public const string ResumeSeekExceedsDurationStartingBeginning =
                "[Resume] Seek position {SeekPosition} exceeds track duration {Duration} - starting from beginning. FinishedDuration may not have been reset after a publication/track change.";

            public const string PlaybackStoppedBeforePlayAborting =
                "Playback was stopped before PlayAsync - aborting PlayCurrentTrackAsync";

            public const string ResumePostPlaySeekExceedsDurationSkippingSeek =
                "[Resume] Post-play validation: seek position {SeekPosition} exceeds track duration {Duration} - skipping seek";

            public const string SeekTotalBudgetExceeded =
                "[Seek] Total time budget of {Budget}s exceeded after {Attempts} attempts — giving up, will play from current position";

            public const string SeekFailedPlayerNotReadySeekableRanges =
                "Seek failed because player isn't ready yet (seekable ranges not available)";

            public const string UnexpectedErrorDuringSeekResumeWillStartBeginning =
                "Unexpected error during seek to resume position, will start from beginning";

            public const string SeekAttemptWasNoOpWillRetry =
                "[Seek] Attempt {Attempt} was NO-OP - Target: {Target}, Current: {Current}, will retry";

            public const string SeekAllAttemptsNoOpStartingBeginning =
                "[Seek] All {MaxRetries} attempts were no-ops, will start from beginning";

            public const string SeekAttemptFailedInvalidOperation =
                "[Seek] Attempt {Attempt} FAILED (InvalidOperationException): {Message}";

            public const string SeekAllAttemptsFailedStartingBeginning =
                "[Seek] All {MaxRetries} attempts FAILED, will start from beginning";

            public const string SeekAttemptFailedUnexpectedError =
                "[Seek] Attempt {Attempt} FAILED with unexpected error: {Message}";
        }

        /// <summary>On-demand URI prep and next-track pre-download (<c>TrackOnDemandPreparer</c>).</summary>
        public static class TrackOnDemandPreparerDiagnosticsLog
        {
            public const string FailedToResolveTrackUriOnDemand =
                "Failed to resolve track URI on-demand: {Url}";

            public const string PreDownloadingNextTrackBackground =
                "Pre-downloading next track in background: LookUpPath={LookUpPath}, URL={Url}";

            public const string PreDownloadCompleteUriResolved =
                "Pre-download complete, URI resolved for next track: {Uri}";

            public const string PreDownloadNextTrackFailedNonCritical =
                "Pre-download of next track failed (non-critical)";
        }

        /// <summary>Schedule listing title/subtitle helpers (<c>ScheduleDisplayMetadataHelper</c>).</summary>
        public static class ScheduleDisplayMetadataDiagnosticsLog
        {
            public const string FailedToResolveCategoryDisplayName =
                "Failed to resolve category display name for code {CategoryCode}";
        }

        /// <summary>iOS lock screen / Control Center Now Playing (<c>iOSNowPlayingInfoManager</c>).</summary>
        public static class IosNowPlayingDiagnosticsLog
        {
            public const string FailedToUpdateMetadata =
                "[iOS NowPlaying] Failed to update metadata";

            public const string FailedToApplyAsyncArtworkTransitional =
                "[iOS NowPlaying] Failed to apply async artwork (app may be in transitional state)";

            public const string FailedToLoadArtworkAsyncFromUrl =
                "[iOS NowPlaying] Failed to load artwork async from {Url}";

            public const string FailedToUpdatePlaybackPosition =
                "[iOS NowPlaying] Failed to update playback position";

            public const string FailedToUpdatePlaybackStatus =
                "[iOS NowPlaying] Failed to update playback status";

            public const string FailedToUpdateDuration =
                "[iOS NowPlaying] Failed to update duration";

            public const string FailedToClearNowPlayingInfo =
                "[iOS NowPlaying] Failed to clear Now Playing info";

            public const string FailedToSetDefaultMetadata =
                "[iOS NowPlaying] Failed to set default metadata";

            public const string FailedToLoadArtworkSynchronouslyFromUrl =
                "[iOS NowPlaying] Failed to load artwork synchronously from {Url}";

            public const string FailedToDownloadArtworkFromUrl =
                "[iOS NowPlaying] Failed to download artwork from {Url}";
        }

        /// <summary>CarPlay scene delegate (<c>CarPlaySceneDelegate</c>).</summary>
        public static class CarPlayDiagnosticsLog
        {
            public const string ConnectedToInterfaceController =
                "[CarPlay] Connected to CarPlay interface controller";

            public const string ClearedIsRecentlyConnectedFlag =
                "[CarPlay] Cleared IsRecentlyConnected flag (auto-play suppression window ended)";

            public const string RotationServiceNotAvailable =
                "[CarPlay] Rotation service not available (DI may not be ready)";

            public const string ErrorDuringCarPlayConnection =
                "[CarPlay] Error during CarPlay connection";

            public const string DisconnectedFromInterfaceController =
                "[CarPlay] Disconnected from CarPlay interface controller";

            public const string FailedToStopRotationService =
                "[CarPlay] Failed to stop rotation service";

            public const string ErrorDuringCarPlayDisconnection =
                "[CarPlay] Error during CarPlay disconnection";

            public const string SuppressCarPlayFinalizersDisposedDuringTeardown =
                "[CarPlay] SuppressCarPlayFinalizers: disposed during teardown (non-fatal)";

            public const string SuppressCarPlayFinalizersUnexpected =
                "[CarPlay] SuppressCarPlayFinalizers: unexpected (non-fatal)";

            public const string CannotSetRootTemplateInterfaceControllerNull =
                "[CarPlay] Cannot set root template - interface controller is null";

            public const string SuccessfullySetScheduleListAsRootTemplate =
                "[CarPlay] Successfully set schedule list as root template";

            public const string FailedToSetRootTemplateWithError =
                "[CarPlay] Failed to set root template: {Error}";

            public const string ErrorSettingRootTemplate =
                "[CarPlay] Error setting root template";

            public const string NoSchedulesInStateShowingEmpty =
                "[CarPlay] No schedules in state - showing loading/empty state";

            public const string UserTappedSchedule =
                "[CarPlay] User tapped schedule: {Title} (ID: {ScheduleId})";

            public const string CreatedScheduleListTemplateWithCount =
                "[CarPlay] Created schedule list template with {Count} schedules";

            public const string ErrorRefreshingScheduleList =
                "[CarPlay] Error refreshing schedule list";

            public const string SuppressOldSectionFinalizersDisposedDuringTeardown =
                "[CarPlay] SuppressOldSectionFinalizers: disposed during teardown (non-fatal)";

            public const string SuppressOldSectionFinalizersUnexpected =
                "[CarPlay] SuppressOldSectionFinalizers: unexpected (non-fatal)";
        }

        /// <summary><c>AppDelegate</c> lifecycle, notifications, and background fetch.</summary>
        public static class IosAppDelegateDiagnosticsLog
        {
            public const string LogCloseAndFlushFailedCrashPath =
                "Log.CloseAndFlush failed during crash path";

            public const string IosMauiAppCreationFailed =
                "iOS MAUI app creation failed.";

            public const string ReturningCarPlaySceneConfiguration =
                "[AppDelegate] Returning CarPlay scene configuration";

            public const string BaseFinishedLaunchingThrewException =
                "base.FinishedLaunching threw exception.";

            public const string IosApplicationCustomInitializationFailed =
                "iOS application custom initialization failed.";

            public const string FailedToSetupIosBackgroundTasks =
                "Failed to set up iOS background tasks (BGTaskScheduler)";

            public const string ErrorWhenShowingNotificationOnIosActivation =
                "Error when showing notification on iOS activation.";

            public const string FailedToResetBadgeCount =
                "Failed to reset badge count: {Error}";

            public const string ErrorHandlingIosNotificationResponse =
                "Error handling iOS notification response.";

            public const string ErrorHandlingIosNotification =
                "Error handling iOS notification.";

            public const string NotificationTappedStartingPlayback =
                "Notification tapped for schedule {ScheduleId}, starting playback";

            public const string StartedPlaybackFromNotificationTap =
                "Started playback for schedule {ScheduleId} from notification tap";

            public const string ISchedulePlaybackServiceNotAvailableNotificationPlayback =
                "ISchedulePlaybackService not available for notification playback";

            public const string ErrorStartingPlaybackFromNotificationForSchedule =
                "Error starting playback from notification for schedule {ScheduleId}";

            public const string ErrorPerformFetchTask =
                "An error occurred in doing perform fetch task.";
        }

        /// <summary>iOS AVAudioSession setup (<c>IOsAudioSessionHelper</c>).</summary>
        public static class IosAudioSessionDiagnosticsLog
        {
            public const string AttemptingToConfigureAudioSessionForContext =
                "Attempting to configure iOS audio session for {Context}.";

            public const string FailedToSetAvAudioSessionCategoryForContext =
                "Failed to set AVAudioSession category for {Context}: {Error}";

            public const string SuccessfullySetAvAudioSessionCategoryPlaybackForContext =
                "Successfully set AVAudioSession category to Playback for {Context}";

            public const string FailedToActivateAvAudioSessionForContext =
                "Failed to activate AVAudioSession for {Context}: {Error}";

            public const string SuccessfullyActivatedAvAudioSessionForContext =
                "Successfully activated AVAudioSession for {Context}";

            public const string ErrorConfiguringIosAudioSessionForContext =
                "Error configuring iOS audio session for {Context}";

            public const string AudioRouteChangedOldDeviceUnavailablePausingPlayback =
                "Audio route changed (old device unavailable, e.g. Bluetooth disconnected) — pausing playback";

            public const string ErrorHandlingAudioRouteChangeNotification =
                "Error handling audio route change notification";

            public const string RegisteredIosAudioRouteChangeObserver =
                "Registered iOS audio route change observer";
        }

        /// <summary>Android Auto media browser service (<c>LegacyMediaBrowserService</c>).</summary>
        public static class LegacyMediaBrowserDiagnosticsLog
        {
            public const string OnCreateCalled =
                "LegacyMediaBrowserService.OnCreate() called";

            public const string OnCreateRotationServiceNotAvailable =
                "LegacyMediaBrowserService.OnCreate: rotation service not available (DI may not be ready)";

            public const string OnCreateErrorDuringInitialization =
                "LegacyMediaBrowserService.OnCreate: error during initialization (foreground service is already running)";

            public const string AndroidAutoClientValidatedMarkingConnected =
                "Android Auto client validated - marking as connected";

            public const string OnLoadChildrenCalledForParent =
                "✅ OnLoadChildren called for parent: {ParentId}";

            public const string ResultDetachedSuccessfullyForParent =
                "Result detached successfully for parent: {ParentId}";

            public const string CriticalErrorOnLoadChildrenBeforeDetach =
                "Critical error in OnLoadChildren before detaching result for parent: {ParentId}";

            public const string StartingScheduleLoadingForParent =
                "Starting schedule loading for parent: {ParentId}";

            public const string CreatedMediaItemsForAndroidAuto =
                "Created {Count} MediaItems for Android Auto";

            public const string NoMediaItemsToSendForParent =
                "No MediaItems to send for parent: {ParentId}";

            public const string ErrorLoadingChildrenForParent =
                "Error loading children in LegacyMediaBrowserService for parent: {ParentId}. Bootstrap may not have completed or services may not be available.";

            public const string PlaybackActiveOnCarConnectSkippingRefresh =
                "Playback is active on car connect - skipping default metadata refresh";

            public const string StoppedStuckMinimalForegroundNotification =
                "Stopped stuck minimal foreground notification — MediaElement is handling playback";

            public const string DefaultMetadataRefreshDispatchedOnCarConnect =
                "Default metadata refresh and browse tree update dispatched on car connect";

            public const string FailedToRefreshDefaultMetadataOnCarConnect =
                "Failed to refresh default metadata on car connect";

            public const string SentEmptyResultForParent =
                "Sent empty result for parent: {ParentId}";

            public const string FailedToSendEmptyResultForParent =
                "Failed to send empty result for parent: {ParentId}. This may cause Android Auto connection issues.";

            public const string OnBindCalledWithIntent =
                "✅ LegacyMediaBrowserService.OnBind() called with intent: {Action}";

            public const string IntentComponentPackageCategories =
                "Intent component: {Component}, Package: {Package}, Categories: {Categories}";

            public const string ServiceStartedToKeepAliveDuringAaConnection =
                "Service started to keep it alive during Android Auto connection";

            public const string FailedToStartServiceInOnBind =
                "Failed to start service in OnBind() - service may be destroyed if Android Auto unbinds";

            public const string SessionTokenSetInOnBind =
                "SessionToken set in OnBind(): {Token}";

            public const string CouldNotSetSessionTokenInOnBind =
                "Could not set SessionToken in OnBind() - bootstrap may still be running";

            public const string OnUnbindCalledClientDisconnected =
                "⚠️ LegacyMediaBrowserService.OnUnbind() called - Client disconnected";

            public const string OnUnbindFailedToStopRotationService =
                "LegacyMediaBrowserService.OnUnbind: failed to stop rotation service";

            public const string OnDestroyClearingLocalMediaSessionReference =
                "✅ LegacyMediaBrowserService destroyed - Clearing local MediaSession reference";

            public const string OnDestroyFailedToStopRotationService =
                "LegacyMediaBrowserService.OnDestroy: failed to stop rotation service";
        }

        /// <summary>Legacy AA browse state subscription (<c>StateSubscriptionManager</c>).</summary>
        public static class LegacyMediaBrowserStateSubscriptionDiagnosticsLog
        {
            public const string BootstrapTimedOutOnCreateWillRetry =
                "Bootstrap timed out in LegacyMediaBrowserService.OnCreate - will retry when schedules are loaded";

            public const string SubscribedToScheduleListChanges =
                "✅ LegacyMediaBrowserService subscribed to schedule list changes";

            public const string ApplicationStateNotAvailable =
                "IState<ApplicationState> not available - schedule updates will not refresh Android Auto UI";

            public const string OnApplicationStateChangedScheduleChangeTrackerNullSkipping =
                "OnApplicationStateChanged: scheduleChangeTracker is null, skipping";

            public const string OnApplicationStateChangedCheckingForScheduleChanges =
                "OnApplicationStateChanged: Checking for schedule changes";

            public const string OnApplicationStateChangedNoChangesDetected =
                "OnApplicationStateChanged: No changes detected (changes is null or empty)";

            public const string OnApplicationStateChangedDetectedScheduleChangesNotifying =
                "OnApplicationStateChanged: Detected {Count} schedule changes, notifying Android Auto";

            public const string OnApplicationStateChangedMediaBrowserServiceNull =
                "OnApplicationStateChanged: MediaBrowserService is null, cannot notify Android Auto of changes";

            public const string ErrorCheckingScheduleListChangesFallbackRefresh =
                "Error checking schedule list changes - falling back to full refresh";

            public const string DetectedScheduleAdded =
                "Detected schedule added: {ScheduleId}";

            public const string DetectedScheduleRemoved =
                "Detected schedule removed: {ScheduleId}";

            public const string DetectedScheduleUpdated =
                "Detected schedule updated: {ScheduleId}";

            public const string NotifiedAndroidAutoOfScheduleChanges =
                "Notified Android Auto of schedule changes: {ChangeCount} changes ({AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed)";

            public const string ErrorPerformingFallbackFullRefresh =
                "Error performing fallback full refresh";
        }

        /// <summary>Legacy AA MediaSession/bootstrap init (<c>MediaSessionInitializer</c>).</summary>
        public static class LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog
        {
            public const string ErrorInitializingMediaSessionWillRetryWhenBound =
                "Error initializing MediaSession in LegacyMediaBrowserService - will retry when service is bound";

            public const string MediaSessionManagerNullCannotCreateMediaSession =
                "MediaSessionManager is null - cannot create MediaSession";

            public const string MediaSessionCompatNullAfterGetOrCreateCannotSetSessionToken =
                "MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken";

            public const string MediaSessionCompatSessionTokenNullMayNotBeInitialized =
                "MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized";

            public const string SessionTokenSuccessfullySet =
                "SessionToken successfully set: {Token}";

            public const string OnCreateCompletedLegacyAaConnectingSessionTokenOk =
                "✅ LegacyMediaBrowserService.OnCreate() completed - Legacy Android Auto is connecting! SessionToken set correctly.";

            public const string OnCreateCompletedBootstrapInitializationStarted =
                "✅ LegacyMediaBrowserService.OnCreate() completed - Bootstrap initialization started";

            public const string BootstrapInitializationCompletedForLegacyMediaBrowser =
                "Bootstrap initialization completed for LegacyMediaBrowserService";

            public const string ErrorInBootstrapInitialization =
                "Error in bootstrap initialization";

            public const string ErrorInitializingBootstrapInLegacyMediaBrowser =
                "Error initializing bootstrap in LegacyMediaBrowserService";
        }

        /// <summary>Legacy AA browse tree (<c>MediaBrowser</c> helper).</summary>
        public static class LegacyMediaBrowserBrowseOperationsDiagnosticsLog
        {
            public const string LoadingChildrenForParentId =
                "Loading children for parent ID: {ParentId}";

            public const string BootstrapCompletedLoadingSchedulesFromStateForParent =
                "Bootstrap completed, loading schedules from state for parent: {ParentId}";

            public const string BootstrapNotReadyOrTimedOutForParentReturningEmptyList =
                "Bootstrap not ready or timed out for parent: {ParentId} - returning empty list";

            public const string NoSchedulesFoundInStateForParentMayNotBeInitialized =
                "No schedules found in state for parent: {ParentId} - state may not be initialized yet";

            public const string LoadChildrenCalledSynchronouslyForParentReturningEmptyList =
                "LoadChildren called synchronously for parent ID: {ParentId} - returning empty list";

            public const string AddedMediaItemForScheduleTitle =
                "Added MediaItem for schedule: {ScheduleId} - Title: {Title}";

            public const string FailedToCreateMediaItemForSchedule =
                "Failed to create MediaItem for schedule {ScheduleId}";

            public const string SetSectionIconBitmapForScheduleSize =
                "Set section icon bitmap for schedule {ScheduleId} - Size: {Width}x{Height}";

            public const string FailedToCreateSectionIconBitmapForSchedule =
                "Failed to create section icon bitmap for schedule {ScheduleId}";

            public const string CreatedSectionIconBitmapSize =
                "Created section icon bitmap - Size: {WidthPx}x{HeightPx}";

            public const string FailedToCreateSectionIconBitmapItemsWillDisplayWithoutIcon =
                "Failed to create section icon bitmap - MediaItems will display without icon";

            public const string CouldNotGetAppDrawableForSectionIcon =
                "Could not get app drawable for section icon";

            public const string GettingMediaItemForId =
                "Getting media item for ID: {MediaId}";

            public const string SearchingForQuery =
                "Searching for: {Query}";
        }

        /// <summary>Legacy AA MediaBrowser client validation (<c>ClientValidator</c>).</summary>
        public static class LegacyMediaBrowserClientValidatorDiagnosticsLog
        {
            public const string OnGetRootCalledForClient =
                "✅ OnGetRoot called for client: {ClientPackageName} (UID: {ClientUid})";

            public const string RejectingMediaBrowserClientNonCarHost =
                "Rejecting MediaBrowser client (non-car host): {ClientPackageName}";
        }

        /// <summary>Legacy AA playback stub (<c>PlaybackController</c>).</summary>
        public static class LegacyMediaBrowserPlaybackControllerDiagnosticsLog
        {
            public const string HandlingPlayCommand =
                "Handling play command";

            public const string HandlingPauseCommand =
                "Handling pause command";

            public const string HandlingSkipToNextCommand =
                "Handling skip to next command";

            public const string HandlingSkipToPreviousCommand =
                "Handling skip to previous command";

            public const string HandlingSeekToPosition =
                "Handling seek to position: {Position}";

            public const string HandlingPlayFromMediaId =
                "Handling play from media ID: {MediaId}";
        }

        /// <summary>Android Auto schedule loading from Fluxor (<c>AndroidAutoScheduleHelper</c>).</summary>
        public static class AndroidAutoScheduleHelperDiagnosticsLog
        {
            public const string LoadingSchedulesFromStateForAa =
                "Loading schedules from state for Android Auto";

            public const string LoadedSchedulesFromStateForAa =
                "Loaded {Count} schedules from state for Android Auto";

            public const string NoSchedulesFoundInStateMayNotBeInitialized =
                "No schedules found in state - state may not be initialized yet";

            public const string ErrorLoadingSchedulesFromStateForAa =
                "Error loading schedules from state for Android Auto";
        }

        /// <summary>Android Auto schedule diff tracker (<c>AndroidAutoScheduleChangeTracker</c>).</summary>
        public static class AndroidAutoScheduleChangeTrackerDiagnosticsLog
        {
            public const string InitializedWithScheduleCount =
                "AndroidAutoScheduleChangeTracker initialized with {Count} schedules";

            public const string GetSpecificChangesNoChangesDetected =
                "GetSpecificChanges: No changes detected (count: {Count}, signatures equal)";

            public const string GetSpecificChangesChangesDetected =
                "GetSpecificChanges: Changes detected (count: {OldCount} -> {NewCount})";

            public const string DetectedScheduleUpdatedSignatures =
                "Detected schedule updated: {ScheduleId} - Old signature: '{OldSignature}', New signature: '{NewSignature}'";
        }

        /// <summary>Android Auto default schedule rotation (<c>AndroidAutoDefaultScheduleRotationService</c>).</summary>
        public static class AndroidAutoDefaultScheduleRotationDiagnosticsLog
        {
            public const string RotationAlreadyStarted =
                "Android Auto default schedule rotation already started";

            public const string RotationStartedEveryMinutesWhenCarConnected =
                "Android Auto default schedule rotation started (every {Minutes} min when car connected and not playing)";

            public const string ErrorStoppingRotation =
                "Error stopping Android Auto default schedule rotation";

            public const string RotationStopped =
                "Android Auto default schedule rotation stopped";

            public const string CarDisconnectedBindFlagStaleCleanup =
                "Car physically disconnected (CarConnection provider) but MediaBrowser bind flag still true — cleaning up stale Android Auto state";

            public const string DispatchingRotateDefaultScheduleActionFiveMinute =
                "Dispatching RotateDefaultScheduleAction for 5-minute rotation";
        }

        /// <summary>Android Auto play screen UI helpers (<c>AndroidAutoPlayScreenHelper</c>).</summary>
        public static class AndroidAutoPlayScreenDiagnosticsLog
        {
            public const string ErrorLoadingAppIconFallbackArtwork =
                "[AndroidAuto] Error loading app icon fallback artwork";
        }

        /// <summary>Modal scroll helper error paths (<c>ModalScrollHelper</c>).</summary>
        public static class ModalUiDiagnosticsLog
        {
            public const string HandleModalAppearingAsyncError =
                "Error in ModalScrollHelper.HandleModalAppearingAsync";

            public const string ModalCancellationTokenDisposalWarning =
                "Error during modal cancellation token disposal";
        }

        /// <summary>Android MediaSession artwork bitmap loads.</summary>
        public static class AndroidMediaArtworkLog
        {
            public const string ErrorLoadingBitmapFromArtworkUrl =
                "Error loading artwork bitmap from: {ArtworkUrl}";

            public const string ErrorLoadingBitmapFromArtworkUrlOmittingArtwork =
                "Error loading artwork bitmap from: {ArtworkUrl} — omitting artwork";
        }

        /// <summary>Android alarm foreground bootstrap (<c>AlarmSetupService</c>, <c>SchedulerJob</c>).</summary>
        public static class AndroidAlarmBootstrapLog
        {
            public const string SchedulerTaskHandlingFailed =
                "An error happened in handling scheduler task.";

            public const string AlarmSetupTaskFailed =
                "An error happened in alarm setup task.";

            public const string SchedulerJobProcessingFailed =
                "Error processing scheduled tasks";
        }

        /// <summary>Schedule persistence / load flows.</summary>
        public static class SchedulePersistenceDiagnosticsLog
        {
            public const string SaveScheduleAsyncErrorSaving =
                "SaveScheduleAsync: Error saving schedule. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}, Name={Name}";

            public const string LoadExistingScheduleAsyncErrorLoadingTemplate =
                "LoadExistingScheduleAsync: Error loading schedule {ScheduleId}";

            public const string ErrorDeletingSchedule =
                "Error deleting schedule {ScheduleId}";

            public const string SaveScheduleAsyncSaveCompletedSuccessfully =
                "SaveScheduleAsync: Save completed successfully. ScheduleId={ScheduleId}";

            public const string SaveScheduleAsyncStarting =
                "SaveScheduleAsync: Starting. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}, HasMusic={HasMusic}, HasBiblePublication={HasBiblePublication}";

            public const string SaveScheduleAsyncSavingNewScheduleToDatabase =
                "SaveScheduleAsync: Saving new schedule to database";

            public const string SaveScheduleAsyncAddingScheduleToDbContext =
                "SaveScheduleAsync: Adding schedule to DbContext. ScheduleId={ScheduleId}, Name={Name}";

            public const string SaveScheduleAsyncSaveChangesAsyncCompletedNewScheduleId =
                "SaveScheduleAsync: SaveChangesAsync completed. New ScheduleId={ScheduleId}";

            public const string SaveScheduleAsyncReloadedSchedule =
                "SaveScheduleAsync: Reloaded schedule. ScheduleId={ScheduleId}, Name={Name}, HasMusic={HasMusic}, HasBiblePublication={HasBiblePublication}";

            public const string SaveScheduleAsyncDispatchingAddScheduleAction =
                "SaveScheduleAsync: Dispatching AddScheduleAction";

            public const string SaveScheduleAsyncAddScheduleActionDispatchedSuccessfully =
                "SaveScheduleAsync: AddScheduleAction dispatched successfully";

            public const string CannotDeleteScheduleLastInDatabase =
                "Cannot delete schedule {ScheduleId} - it is the last schedule in the database";
        }

        /// <summary>Playback pipeline and modal adapter error paths.</summary>
        public static class PlaybackDiagnosticsLog
        {
            public const string ErrorInHandleMediaFailedAsync = "Error in HandleMediaFailedAsync";

            public const string FailureRecoveryInHandleMediaFailedCatch =
                "Failure recovery in HandleMediaFailedAsync catch";

            public const string ErrorMarkingTrackAsFinished = "Error marking track as finished";

            public const string ErrorHandlingMediaEndedEvent = "Error handling media ended event";

            public const string ErrorHandlingMediaFailedEvent = "Error handling media failed event";

            public const string FailureRecoveryAfterMediaFailedHandlerError =
                "Failure recovery after media failed handler error";
        }

        /// <summary><c>MediaElement</c> event handler diagnostics (<c>EventHandlerManager</c>).</summary>
        public static class MediaElementHandlerDiagnosticsLog
        {
            public const string OnMediaOpenedMayBeDisposed =
                "Error in OnMediaOpened handler (MediaElement may have been disposed)";

            public const string OnMediaEndedMayBeDisposed =
                "Error in OnMediaEnded handler (MediaElement may have been disposed)";

            public const string OnMediaFailedInvokingFailureCallback =
                "Error in OnMediaFailed handler - invoking failure callback to ensure graceful recovery";

            public const string OnMediaFailedFailureCallbackThrew =
                "Failure callback also threw - app may show inconsistent state";

            public const string OnStateChangedMayBeDisposed =
                "Error in OnStateChanged handler (MediaElement may have been disposed)";

            public const string OnPositionChangedMayBeDisposed =
                "Error in OnPositionChanged handler (MediaElement may have been disposed)";

            public const string OnSeekCompletedMayBeDisposed =
                "Error in OnSeekCompleted handler (MediaElement may have been disposed)";

            public const string UnsubscribedFromMediaElementEvents =
                "Unsubscribed from MediaElement events";

            public const string ErrorUnsubscribingFromMediaElementEventsMayHaveBeenDisposed =
                "Error unsubscribing from MediaElement events (may have been disposed)";

            public const string MediaElementFailedToPlayTrackUriSource =
                "MediaElement failed to play track. URI: {TrackUri}, Source: {Source}";

            public const string AudioPlayerOnPositionChangedDuringSeekCurrentPosition =
                "[AudioPlayer] OnPositionChanged during seek - CurrentPosition: {Position}";

            public const string AudioPlayerOnSeekCompletedEventFiredResettingSeeking =
                "[AudioPlayer] OnSeekCompleted event fired - CurrentPosition: {Position}, Resetting _isSeeking = false";

            public const string AudioPlayerSeekCompletedResumingNormalPositionAndStatusUpdates =
                "[AudioPlayer] Seek completed, resuming normal position and status updates";
        }

        /// <summary>Schedule list item VM initialization.</summary>
        public static class ScheduleListItemDiagnosticsLog
        {
            public const string InitializeFromScheduleInvalidScheduleOrId =
                "InitializeFromSchedule: Invalid schedule or schedule ID {ScheduleId}";

            public const string SetScheduleIdInvalidScheduleId =
                "SetScheduleId: Invalid schedule ID {ScheduleId}";
        }

        /// <summary>Category selection / browsing.</summary>
        public static class CategorySelectionDiagnosticsLog
        {
            public const string ErrorLoadingCategories = "Error loading categories";
        }

        /// <summary>Android notification service secondary errors.</summary>
        public static class AndroidNotificationServiceDiagnosticsLog
        {
            public const string FailedToShowToastExactAlarmPermissionError =
                "Failed to show toast message for exact alarm permission error";
        }

        /// <summary>Android MediaSession creation failures (service, receivers, legacy browser).</summary>
        public static class AndroidMediaSessionCreationDiagnosticsLog
        {
            public const string AlarmSetupServiceOnCreateFailed =
                "AlarmSetupService.OnCreate: failed to create MediaSession";

            public const string AlarmSetupServiceOnStartCommandFailed =
                "AlarmSetupService.OnStartCommand: failed to create MediaSession";

            public const string RestartReceiverOnReceiveFailed =
                "RestartReceiver.OnReceive: failed to create MediaSession";

            public const string AlarmRingerReceiverOnReceiveFailed =
                "AlarmRingerReceiver.OnReceive: failed to create MediaSession";

            public const string LegacyMediaBrowserServiceOnCreateFailed =
                "LegacyMediaBrowserService.OnCreate: failed to create MediaSession";
        }

        /// <summary><c>MediaSessionCompat</c> sync (<c>MediaSessionEffect</c>).</summary>
        public static class AndroidMediaSessionCompatUpdateDiagnosticsLog
        {
            public const string ErrorUpdatingPlaybackState =
                "Error updating MediaSessionCompat playback state";

            public const string ErrorUpdatingMetadata =
                "Error updating MediaSessionCompat metadata";

            public const string ErrorUpdatingDefaultScheduleMetadata =
                "Error updating MediaSessionCompat with default schedule metadata";

            public const string ErrorUpdatingDuration =
                "Error updating MediaSessionCompat duration";

            public const string ErrorUpdatingNavigationState =
                "Error updating MediaSessionCompat navigation state";

            public const string ErrorUpdatingPlaybackPosition =
                "Error updating MediaSessionCompat playback position";
        }

        /// <summary><c>NavigationService</c> error and teardown diagnostics.</summary>
        public static class NavigationServiceDiagnosticsLog
        {
            public const string NavigateToScheduleAsyncFailed = "NavigateToScheduleAsync failed";

            public const string NavigateToScheduleAsyncWithScheduleIdFailed =
                "NavigateToScheduleAsync(scheduleId, isEnabled) failed";

            public const string PerfNavigateToScheduleAsyncStartAt =
                "[PERF] NavigateToScheduleAsync: Start at {StartTime}";

            public const string PerfNavigateToScheduleAsyncLockAcquiredInMs =
                "[PERF] NavigateToScheduleAsync: Lock acquired in {ElapsedMs}ms";

            public const string PerfNavigateToScheduleAsyncBeforeShellPageResolveAt =
                "[PERF] NavigateToScheduleAsync: Before shell page resolve at {Time}";

            public const string PerfNavigateToScheduleAsyncShellPageResolvedInMs =
                "[PERF] NavigateToScheduleAsync: Shell page resolved in {ElapsedMs}ms";

            public const string PerfNavigateToScheduleAsyncBeforePushAt =
                "[PERF] NavigateToScheduleAsync: Before push at {Time}";

            public const string PerfNavigateToScheduleAsyncPushCompletedTotalSoFarMs =
                "[PERF] NavigateToScheduleAsync: Push completed in {ElapsedMs}ms, total so far: {TotalMs}ms";

            public const string PerfNavigateToScheduleAsyncResolvingViewModelAt =
                "[PERF] NavigateToScheduleAsync: Resolving ViewModel at {Time}";

            public const string PerfNavigateToScheduleAsyncViewModelResolvedInMs =
                "[PERF] NavigateToScheduleAsync: ViewModel resolved in {ElapsedMs}ms";

            public const string PerfNavigateToScheduleAsyncInitializeViewModelCompleteTotalMs =
                "[PERF] NavigateToScheduleAsync: InitializeViewModelAsync complete, total: {TotalMs}ms";

            public const string IsPlaybackModalOnScreenNavigationUnavailable =
                "IsPlaybackModalOnScreen: navigation unavailable, assuming modal not shown";

            public const string ErrorDisposingPlaybackModalNonFatal =
                "Error disposing PlaybackModal (non-fatal)";

            public const string ErrorCleaningUpIosNativeViewsPlaybackModalNonFatal =
                "Error cleaning up iOS native views for PlaybackModal (non-fatal)";

            public const string ErrorDuringNavigationLockDisposal =
                "Error during navigation lock disposal";

            public const string PopAllModalsAndPagesFinishedDisposing =
                "NavigationService.PopAllModalsAndPages - Finished disposing modals and pages. Modal count: {ModalCount}, Page count: {PageCount}";

            public const string PopAllModalsAndPagesErrorDuringCleanup =
                "NavigationService.PopAllModalsAndPages - Error during modal/page cleanup";

            public const string PopAllModalsAndPagesCouldNotGetNavigation =
                "NavigationService.PopAllModalsAndPages - Could not get navigation, fragments may be destroyed";

            public const string PopAllModalsAndPagesCouldNotAccessModalStack =
                "NavigationService.PopAllModalsAndPages - Could not access ModalStack, fragments may be destroyed";

            public const string PopAllModalsAndPagesCouldNotAccessNavigationStack =
                "NavigationService.PopAllModalsAndPages - Could not access NavigationStack, fragments may be destroyed";

            public const string PopAllModalsAndPagesDisposedPage =
                "NavigationService.PopAllModalsAndPages - Disposed {PageType}: {PageTypeName}";

            public const string PopAllModalsAndPagesErrorDisposingPage =
                "NavigationService.PopAllModalsAndPages - Error disposing {PageType}: {PageTypeName}";

            public const string PopAllModalsAndPagesErrorCleaningUpIosNativeViews =
                "NavigationService.PopAllModalsAndPages - Error cleaning up iOS native views for {PageType}: {PageTypeName} (non-fatal)";
        }
    }

    /// <summary>Notification body copy shared across platforms.</summary>
    public static class Notifications
    {
        public const string TapAlarmToListenBody = "Press to start listening now.";
    }

    /// <summary>Cross-platform toast message strings.</summary>
    public static class ToastMessages
    {
        public const string NetworkMayNotBeAvailableTryAgain = "Network may not be available, please try again";
        public const string CannotUpdateTrackScheduleInProgress = "Cannot update the track when schedule is in progress";
        public const string SelectAtLeastOneDay = "Select at least one day";
        public const string ScheduleDataNotReadyTryAgain = "Schedule data is not ready, please try again";
        public const string InvalidScheduleIdTryAgain = "Invalid schedule ID, please try again";
        public const string CannotDeleteLastSchedule = "Cannot delete last schedule";
        public const string ScheduleSaved = "Schedule saved";

        public const string PleaseCheckInternetConnection = "Please check your internet connection";
        public const string NotificationPermissionDeniedByAndroid = "Notification permission is denied by Android";
        public const string RepeatEnabled = "Repeat enabled";

        public const string CannotScheduleReminderExactAlarmPermission =
            "Cannot schedule reminder. Please enable 'Alarms & reminders' permission in system settings.";

        public const string NotificationPermissionRequiredRemindersIos =
            "Notification permission is required for reminders on iOS. Please enable notifications in system settings.";

        public const string NotificationPermissionRequiredTapToPlayWinUi =
            "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.";
    }

    /// <summary>Notification permission modal primary explanatory text.</summary>
    public static class NotificationPermissionModalMessages
    {
        public const string MainAndroidTapToPlayReminders =
            "Notification permission is required for tap-to-play alarms. Please enable notifications to allow the app to show reminder notifications that you can tap to play alarms.";

        public const string MainIosScheduledAlarms =
            "Notification permission is required for alarms to work on iOS. Please enable notifications to allow the app to play alarms at scheduled times.";

        public const string MainOtherPlatforms =
            "Notification permission is required for alarms. Please enable notifications.";

        public const string RequestNotificationPermissionButtonLabel = "REQUEST NOTIFICATION PERMISSION";

        public const string OpenAppSettingsButtonLabel = "OPEN APP SETTINGS";

        public const string OpenSettingsButtonLabel = "OPEN SETTINGS";

        public const string InstructionsAndroidAfterAllowDialog =
            "After clicking the button below, tap 'Allow' in the system permission dialog to enable notifications. If you've previously denied permission, use 'Open App Settings' to enable it in system settings.";

        public const string InstructionsIosAfterAllowDialog =
            "After clicking the button below, tap 'Allow' in the system permission dialog to enable notifications. If you've previously denied permission, use 'Open Settings' to enable it in system settings.";

        public const string InstructionsOtherPlatformsEnableInSettings =
            "Please enable notifications in system settings.";
    }

    /// <summary>Sample schedule seeding — exception text and substring filters for sample schedule creation.</summary>
    public static class SampleScheduleDiagnostics
    {
        public const string MessageContainsNoBiblePublications = "No Bible publications found";
        public const string MessageContainsNoSectionedPublication = "No sectioned Bible publication";

        public const string NoBiblePublicationsInDatabaseMessage = "No Bible publications found in database";
        public const string NoSectionedPublicationForSampleScheduleMessage =
            "No sectioned Bible publication found in database for sample schedule";
    }

    /// <summary>CarPlay schedule browse template strings.</summary>
    public static class CarPlayScheduleList
    {
        public const string SectionTitleSchedules = "Schedules";
        public const string LoadingPrimaryText = "Loading schedules…";
        public const string LoadingSecondaryText = "Schedules will appear once the app is ready";
    }

    /// <summary>
    /// Media-related configuration constants
    /// </summary>
    public static class Media
    {
        /// <summary>
        /// Default language code for English
        /// </summary>
        public const string DefaultLanguageCode = "E";

        /// <summary>English language display name for UI when resolving <see cref="DefaultLanguageCode"/>.</summary>
        public const string DefaultLanguageDisplayNameEnglish = "English";

        /// <summary>
        /// Language code patch for bad data (LAH -> LAHU)
        /// </summary>
        public const string LanguageCodePatchFrom = "LAH";

        /// <summary>
        /// Language code patch for bad data (LAH -> LAHU)
        /// </summary>
        public const string LanguageCodePatchTo = "LAHU";

        /// <summary>
        /// Media file extension for cached files
        /// </summary>
        public const string MediaFileExtension = ".mp3";

        /// <summary>Video/cache file extension for MP4 streams.</summary>
        public const string MediaVideoFileExtension = ".mp4";

        /// <summary>M4A audio file extension.</summary>
        public const string MediaM4aFileExtension = ".m4a";

        /// <summary>AAC audio file extension.</summary>
        public const string MediaAacFileExtension = ".aac";

        /// <summary>Default User-Agent for media downloads and remote artwork/metadata HTTP requests.</summary>
        public const string MediaHttpUserAgent = "BibleAlarm/1.0 (compatible; iOS; MAUI)";

        /// <summary><c>Accept</c> header value meaning any media type.</summary>
        public const string HttpAcceptAny = "*/*";

        /// <summary>Fragments in HTTP exception messages treated as permanent failures for download retries.</summary>
        public static class DownloadPermanentFailureHttpFragments
        {
            public const string StatusCode403 = "403";
            public const string StatusCode404 = "404";
            public const string Forbidden = "Forbidden";
            public const string NotFound = "Not Found";
        }

        /// <summary>HTTP User-Agent for cataloger tool downloads from JW/CDN.</summary>
        public const string CatalogerHttpUserAgent = "Mozilla/5.0 (compatible; curl/8.0.1)";

        /// <summary><c>Accept</c> header for cataloger JW/API HTTP requests.</summary>
        public const string CatalogerHttpAcceptHeader = "application/json, text/plain, */*";

        /// <summary>JW GETPUB/Mediator JSON files.* stream format key for MP3 (uppercase).</summary>
        public const string MediaStreamFormatMp3 = "MP3";

        /// <summary>JW GETPUB/Mediator JSON files.* stream format key for MP4 (uppercase).</summary>
        public const string MediaStreamFormatMp4 = "MP4";

        /// <summary>Alternate lowercase JSON property sometimes returned for MP3 streams.</summary>
        public const string MediaStreamFormatMp3Lower = "mp3";

        /// <summary>Alternate lowercase JSON property sometimes returned for MP4 streams.</summary>
        public const string MediaStreamFormatMp4Lower = "mp4";

        /// <summary>JW GETPUB query/fileformat key for M4A streams.</summary>
        public const string MediaStreamFormatM4a = "M4A";

        /// <summary>JW GETPUB query/fileformat key for AAC streams.</summary>
        public const string MediaStreamFormatAac = "AAC";

        /// <summary>JW GETPUB/Mediator video <c>files[].label</c> for lowest-tier stream (preferred for audio-only).</summary>
        public const string VideoQualityLabel240p = "240p";

        /// <summary>JSON property names shared across JW GETPUB/Mediator response parsers.</summary>
        public static class PubMediaJson
        {
            public const string Files = "files";
            public const string Category = "category";
            public const string Name = "name";
            public const string PubName = "pubName";
            public const string ParentPubName = "parentPubName";
            public const string FormattedDate = "formattedDate";
            public const string File = "file";
            public const string Url = "url";
            public const string Track = "track";
            public const string Title = "title";
            public const string Text = "text";
            public const string Label = "label";
            public const string ParentCategory = "parentCategory";
            public const string Media = "media";
            public const string PrimaryCategory = "primaryCategory";
            public const string NaturalKey = "naturalKey";
            public const string ProgressiveDownloadUrl = "progressiveDownloadURL";

            /// <summary>Mediator <c>category.media[].availableLanguages</c>.</summary>
            public const string AvailableLanguages = "availableLanguages";

            /// <summary>Mediator <c>category.language</c> (localized metadata for request locale).</summary>
            public const string Language = "language";

            /// <summary>GETPUB flat video file duration field when present.</summary>
            public const string Duration = "duration";

            /// <summary>Some JW hub/index payloads use PascalCase <c>Name</c> on embedded nodes.</summary>
            public const string NamePascal = "Name";
        }

        /// <summary>JSON property names in JW language list / catalog responses (alllangs, language APIs).</summary>
        public static class LanguageIndexJson
        {
            public const string Languages = "languages";
            public const string Data = "data";
            public const string LangCode = "langcode";
            public const string Symbol = "symbol";
            public const string Direction = "direction";
            public const string IsSignLanguage = "isSignLanguage";
        }

        /// <summary>JW GETPUB/MEDIALINKS query segment for JSON responses (<c>output=json</c>).</summary>
        public const string GetPubQueryOutputJson = "output=json";

        /// <summary>GETPUB query segment: single-language mode (<c>alllangs=0</c>).</summary>
        public const string GetPubQueryAllLangsOff = "alllangs=0";

        /// <summary>GETPUB query segment: list all languages (<c>alllangs=1</c>).</summary>
        public const string GetPubQueryAllLangsOn = "alllangs=1";

        /// <summary>GETPUB query parameter name for written language (<c>langwritten</c>).</summary>
        public const string GetPubQueryParamLangWritten = "langwritten";

        /// <summary>GETPUB/MEDIALINKS query parameter names (<c>pub</c>, <c>track</c>, etc.).</summary>
        public static class GetPubQueryParamName
        {
            public const string Pub = "pub";
            public const string BookNum = "booknum";
            public const string Issue = "issue";
            public const string DocId = "docid";
            public const string FileFormat = "fileformat";
            public const string Track = "track";
        }

        /// <summary>Mediator lookup-path query parameter names (<c>category</c>, <c>lang</c>).</summary>
        public static class MediatorQueryParamName
        {
            public const string Category = "category";
            public const string Lang = "lang";
        }

        /// <summary>JW mediator natural-key and section-code prefixes (<c>docid:</c>, <c>docid-</c>, <c>pub-</c>).</summary>
        public static class MediatorIdentifiers
        {
            /// <summary>Section code prefix when keyed by JW doc id (<c>docid:12345</c>).</summary>
            public const string DocIdSectionPrefix = "docid:";

            /// <summary>Natural key prefix for doc-id media (<c>docid-...</c>).</summary>
            public const string DocIdNaturalKeyPrefix = "docid-";

            /// <summary>Natural key prefix for pub-style keys (<c>pub-nwt_E_1_AUDIO</c>).</summary>
            public const string PubNaturalKeyPrefix = "pub-";
        }

        /// <summary>Publication/track picker UI: singular track (sectioned media unit).</summary>
        public const string PublicationUiTrackSingular = "Track";

        /// <summary>Publication/track picker UI: plural tracks.</summary>
        public const string PublicationUiTrackPlural = "Tracks";

        /// <summary>Publication/track picker UI: singular part (flat media unit).</summary>
        public const string PublicationUiPartSingular = "Part";

        /// <summary>Publication/track picker UI: singular episode.</summary>
        public const string PublicationUiEpisodeSingular = "Episode";

        /// <summary>Publication/track picker UI: plural episodes.</summary>
        public const string PublicationUiEpisodePlural = "Episodes";

        /// <summary>Publication/track picker UI: singular chapter.</summary>
        public const string PublicationUiChapterSingular = "Chapter";

        /// <summary>Publication/track picker UI: plural chapters.</summary>
        public const string PublicationUiChapterPlural = "Chapters";

        /// <summary>Sample schedule default name when creating new (<c>AlarmSchedule.GetSampleSchedule</c>).</summary>
        public const string ScheduleUiSampleNameNew = "New schedule";

        /// <summary>Sample schedule placeholder name for templates.</summary>
        public const string ScheduleUiSampleNamePlaceholder = "Schedule Name";

        /// <summary>Display when a non–Bible schedule has no user-provided name (<c>Unnamed schedule</c>).</summary>
        public const string ScheduleUiUnnamedPlaceholder = "Unnamed schedule";

        /// <summary>Short status label for enabled schedules in listing subtitles.</summary>
        public const string ScheduleUiStatusEnabled = "Enabled";

        /// <summary>Short status label for disabled schedules in listing subtitles.</summary>
        public const string ScheduleUiStatusDisabled = "Disabled";

        /// <summary>Bound property name <c>MusicLanguageDisplayText</c> on schedule music selection UI.</summary>
        public const string ScheduleMusicLanguageDisplayTextPropertyName = "MusicLanguageDisplayText";

        /// <summary>User-facing playback modal strings for CDN/stream failures (tap Retry).</summary>
        public static class PlaybackModalMessages
        {
            public const string PlaybackFailedTapRetry = "Playback failed. Tap Retry.";
            public const string CouldNotLoadNextPartCheckConnectionTapRetry = "Could not load the next part. Check your connection, then tap Retry.";
            public const string PlaybackStoppedConnectionLostTapRetry = "Playback stopped (connection lost or interrupted). Tap Retry.";
            public const string CouldNotStartPlaybackNetworkBusyTapRetry = "Could not start playback (network or server busy). Tap Retry.";
            public const string CouldNotUpdatePlaybackLinksTapRetry = "Could not update playback links. Tap Retry.";
            public const string StillCouldNotPlayAfterUpdatingLinksTapRetry = "Still could not play after updating links. Tap Retry.";
            public const string CouldNotStartPlaybackCheckConnectionTapRetry = "Could not start playback. Check your connection, then tap Retry.";
        }

        /// <summary>Fallback strings for Now Playing–style surfaces (Android Auto, notifications, seed metadata).</summary>
        public static class NowPlayingPlaceholder
        {
            public const string ArtistReadyToPlay = "Ready to play";
            public const string ArtistTapToPlay = "Tap to play";
            public const string AlbumEllipsis = "...";
        }

        /// <summary>Melody disc fallback UI: prefix before volume index (e.g. <c>Volume 1</c>).</summary>
        public const string PublicationUiMelodyVolumePrefix = "Volume ";

        /// <summary>English title for Good News According to Jesus (<see cref="BiblePublicationCodeDramasGoodNews"/>).</summary>
        public const string PublicationDisplayNameGoodNewsAccordingToJesus = "The Good News According to Jesus";

        /// <summary>Sentence-case variant for misleading Good News titles in Bible GETPUB responses.</summary>
        public const string ApiMisleadingGoodNewsPublicationPhraseAlternate = "Good news according to Jesus";

        /// <summary>
        /// Substrings in JW GETPUB publication names that indicate video drama titles wrongly returned for Bible publications.
        /// </summary>
        public static readonly string[] ApiMisleadingGoodNewsVideoPublicationNamePhrases =
        {
            PublicationDisplayNameGoodNewsAccordingToJesus,
            ApiMisleadingGoodNewsPublicationPhraseAlternate
        };

        /// <summary>
        /// Bible publication category code for vocal and instrumental music (JW catalog).
        /// </summary>
        public const string BiblePublicationCategoryMusic = "Music";

        /// <summary>JW catalog publication code for Original Songs (vocal).</summary>
        public const string MusicPublicationCodeOsg = "osg";

        /// <summary>JW catalog vocal music publication code Sing Joyfully—Convention Songs.</summary>
        public const string MusicPublicationCodeSjjc = "sjjc";

        /// <summary>JW catalog vocal music publication code <c>sjji</c> (flat MP3).</summary>
        public const string MusicPublicationCodeSjji = "sjji";

        /// <summary>JW catalog vocal music publication code <c>snv</c> (flat MP3).</summary>
        public const string MusicPublicationCodeSnv = "snv";

        /// <summary>JW catalog vocal music publication code <c>pksjj</c> (flat MP3).</summary>
        public const string MusicPublicationCodePksjj = "pksjj";

        /// <summary>JW catalog publication code for Kingdom Melodies (instrumental).</summary>
        public const string MelodyMusicPublicationCodeIam = "iam";

        /// <summary>English fallback display name for Kingdom Melodies (<see cref="MelodyMusicPublicationCodeIam"/>).</summary>
        public const string PublicationDisplayNameKingdomMelodies = "Kingdom Melodies";

        /// <summary>JW catalog publication code for Apply Yourself to Reading and Teaching—Videos (series).</summary>
        public const string SeriesPublicationCodeThv = "thv";

        /// <summary>
        /// Bible publication category code for scripture (JW catalog).
        /// </summary>
        public const string BiblePublicationCategoryBible = "Bible";

        /// <summary>JW catalog publication code for New World Translation (study edition).</summary>
        public const string BiblePublicationCodeNwt = "nwt";

        /// <summary>JW catalog publication code for New World Translation (reference edition).</summary>
        public const string BiblePublicationCodeBi12 = "bi12";

        /// <summary>
        /// Bible book number string for Genesis (<c>booknum=1</c>). Used for validation/sample hints and first-track defaults.
        /// </summary>
        public const string BiblePublicationGenesisBookNumber = "1";

        /// <summary>Cataloger log label for flat Article Series MP3 publications.</summary>
        public const string CatalogFlatAudioLabelArticleSeries = "Article Series";

        /// <summary>Cataloger log label for Books flat MP3 publications.</summary>
        public const string CatalogFlatAudioLabelBooks = "Books";

        /// <summary>Cataloger log label for Yearbooks flat MP3 publications.</summary>
        public const string CatalogFlatAudioLabelYearbooks = "Yearbooks";

        /// <summary>Cataloger log label for Brochures and Booklets flat MP3 publications.</summary>
        public const string CatalogFlatAudioLabelBrochuresAndBooklets = "Brochures and Booklets";

        /// <summary>
        /// Bible publication category code for audio/video dramas (JW catalog).
        /// </summary>
        public const string BiblePublicationCategoryDramas = "Dramas";

        /// <summary>
        /// Canonical JW publication code for Dramatic Bible Readings (DB/API; used with drama publication normalization).
        /// </summary>
        public const string BiblePublicationCodeDramaticBibleReadings = "DramaticBibleReadings";

        /// <summary>
        /// JW catalog category code for Watchtower magazine content.
        /// </summary>
        public const string BiblePublicationCategoryWatchtowerMagazine = "WatchtowerMagazine";

        /// <summary>
        /// JW catalog category code for Awake! magazine content.
        /// </summary>
        public const string BiblePublicationCategoryAwakeMagazine = "AwakeMagazine";

        /// <summary>
        /// Canonical JW publication code for Good News According to Jesus (video; Mediator API).
        /// </summary>
        public const string BiblePublicationCodeDramasGoodNews = "DramasGoodNews";

        /// <summary>
        /// Mediator publication code: VOD Bible Times videos.
        /// </summary>
        public const string BiblePublicationCodeVODMoviesBibleTimes = "VODMoviesBibleTimes";

        /// <summary>
        /// Mediator publication code: VOD Modern-Day videos.
        /// </summary>
        public const string BiblePublicationCodeVODMoviesModernDay = "VODMoviesModernDay";

        /// <summary>
        /// Mediator publication code: VOD Animated videos.
        /// </summary>
        public const string BiblePublicationCodeVODMoviesAnimated = "VODMoviesAnimated";

        /// <summary>
        /// Mediator publication code: VOD Extras videos.
        /// </summary>
        public const string BiblePublicationCodeVODMoviesExtras = "VODMoviesExtras";

        /// <summary>
        /// Mediator publication code: Dig for Treasures series.
        /// </summary>
        public const string BiblePublicationCodeSeriesDigForTreasures = "SeriesDigForTreasures";

        /// <summary>
        /// Mediator publication code: Become Jehovah's Friend lessons series.
        /// </summary>
        public const string BiblePublicationCodeSeriesBJFLessons = "SeriesBJFLessons";

        /// <summary>
        /// Mediator publication code: What My Peers Say series.
        /// </summary>
        public const string BiblePublicationCodeSeriesWhatPeersSay = "SeriesWhatPeersSay";

        /// <summary>JW catalog category code: Faith and Bible.</summary>
        public const string BiblePublicationCategoryFaithAndBible = "FaithAndBible";

        /// <summary>JW catalog category code: Books.</summary>
        public const string BiblePublicationCategoryBooks = "Books";

        /// <summary>JW catalog category code: Yearbooks.</summary>
        public const string BiblePublicationCategoryYearbooks = "Yearbooks";

        /// <summary>JW catalog category code: Broadcasting.</summary>
        public const string BiblePublicationCategoryBroadcasting = "Broadcasting";

        /// <summary>JW catalog category code: Brochures and booklets.</summary>
        public const string BiblePublicationCategoryBrochuresAndBooklets = "BrochuresAndBooklets";

        /// <summary>JW catalog category code: Children.</summary>
        public const string BiblePublicationCategoryChildren = "Children";

        /// <summary>JW catalog category code: Family.</summary>
        public const string BiblePublicationCategoryFamily = "Family";

        /// <summary>JW catalog category code: Interviews and experiences.</summary>
        public const string BiblePublicationCategoryInterviewsAndExperiences = "InterviewsAndExperiences";

        /// <summary>JW catalog category code: Meetings and ministry.</summary>
        public const string BiblePublicationCategoryMeetingsAndMinistry = "MeetingsAndMinistry";

        /// <summary>JW catalog category code: Programs and events.</summary>
        public const string BiblePublicationCategoryProgramsAndEvents = "ProgramsAndEvents";

        /// <summary>JW catalog category code: Series.</summary>
        public const string BiblePublicationCategorySeries = "Series";

        /// <summary>JW catalog category code: Teenagers.</summary>
        public const string BiblePublicationCategoryTeenagers = "Teenagers";

        /// <summary>JW catalog category code: Activities.</summary>
        public const string BiblePublicationCategoryActivities = "Activities";

        /// <summary>JW catalog category code: Organization.</summary>
        public const string BiblePublicationCategoryOrganization = "Organization";

        /// <summary>JW catalog category code: Article series.</summary>
        public const string BiblePublicationCategoryArticleSeries = "ArticleSeries";

        /// <summary>Mediator API category key (2014 regional convention).</summary>
        public const string MediatorCategoryKey2014Convention = "2014Convention";

        /// <summary>Mediator API category key (2015 regional convention).</summary>
        public const string MediatorCategoryKey2015Convention = "2015Convention";

        /// <summary>Mediator API category key (2016 regional convention).</summary>
        public const string MediatorCategoryKey2016Convention = "2016Convention";

        /// <summary>Mediator API category key (2017 regional convention).</summary>
        public const string MediatorCategoryKey2017Convention = "2017Convention";

        /// <summary>Mediator API category key (2018 regional convention).</summary>
        public const string MediatorCategoryKey2018Convention = "2018Convention";

        /// <summary>Mediator API category key (2019 regional convention).</summary>
        public const string MediatorCategoryKey2019Convention = "2019Convention";

        /// <summary>Mediator API category key (2020 regional convention).</summary>
        public const string MediatorCategoryKey2020Convention = "2020Convention";

        /// <summary>Mediator API category key (2021 regional convention).</summary>
        public const string MediatorCategoryKey2021Convention = "2021Convention";

        /// <summary>Mediator API category key (2022 regional convention).</summary>
        public const string MediatorCategoryKey2022Convention = "2022Convention";

        /// <summary>Mediator API category key (2023 regional convention).</summary>
        public const string MediatorCategoryKey2023Convention = "2023Convention";

        /// <summary>Mediator API category key (2024 regional convention).</summary>
        public const string MediatorCategoryKey2024Convention = "2024Convention";

        /// <summary>Mediator API category key (2025 regional convention).</summary>
        public const string MediatorCategoryKey2025Convention = "2025Convention";

        /// <summary>Mediator API aggregator category key (children).</summary>
        public const string MediatorCategoryKeyChildrenMovies = "ChildrenMovies";

        /// <summary>Mediator API aggregator category key (children).</summary>
        public const string MediatorCategoryKeyChildrenSongs = "ChildrenSongs";

        /// <summary>Mediator API aggregator category key (family).</summary>
        public const string MediatorCategoryKeyFamilyMovies = "FamilyMovies";

        /// <summary>Mediator API aggregator category key (family).</summary>
        public const string MediatorCategoryKeyFamilyWorship = "FamilyWorship";

        /// <summary>Mediator API aggregator category key (teen).</summary>
        public const string MediatorCategoryKeyTeenMovies = "TeenMovies";

        /// <summary>Mediator API aggregator category key (teen).</summary>
        public const string MediatorCategoryKeyTeenSocialLife = "TeenSocialLife";

        /// <summary>Mediator API aggregator category key (teen).</summary>
        public const string MediatorCategoryKeyTeenGoals = "TeenGoals";

        /// <summary>Mediator API aggregator category key (teen).</summary>
        public const string MediatorCategoryKeyTeenSpiritualGrowth = "TeenSpiritualGrowth";

        /// <summary>Mediator API aggregator category key (teen).</summary>
        public const string MediatorCategoryKeyTeenWhatPeersSay = "TeenWhatPeersSay";

        /// <summary>Mediator API category key (Bible books).</summary>
        public const string MediatorCategoryKeyBibleBooks = "BibleBooks";

        /// <summary>Mediator API primary category key (series Bible books).</summary>
        public const string MediatorCategoryKeySeriesBibleBooks = "SeriesBibleBooks";

        /// <summary>Mediator publication code (broadcasting studio).</summary>
        public const string MediatorPublicationCodeStudioMonthlyPrograms = "StudioMonthlyPrograms";

        /// <summary>Mediator publication code (broadcasting studio).</summary>
        public const string MediatorPublicationCodeStudioTalks = "StudioTalks";

        /// <summary>Mediator publication code (broadcasting studio).</summary>
        public const string MediatorPublicationCodeStudioNewsReports = "StudioNewsReports";

        /// <summary>Mediator publication code (programs and events).</summary>
        public const string MediatorPublicationCodeVODPgmEvtMorningWorship = "VODPgmEvtMorningWorship";

        /// <summary>Mediator publication code (programs and events).</summary>
        public const string MediatorPublicationCodeVODPgmEvtSpecial = "VODPgmEvtSpecial";

        /// <summary>Mediator publication code (programs and events).</summary>
        public const string MediatorPublicationCodeVODPgmEvtGilead = "VODPgmEvtGilead";

        /// <summary>Mediator publication code (programs and events).</summary>
        public const string MediatorPublicationCodeVODPgmEvtAnnMtg = "VODPgmEvtAnnMtg";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBibleReadingStudy = "VODBibleReadingStudy";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBibleTeachings = "VODBibleTeachings";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBibleAccounts = "VODBibleAccounts";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBibleMedia = "VODBibleMedia";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBibleTranslations = "VODBibleTranslations";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBiblePrinciples = "VODBiblePrinciples";

        /// <summary>Mediator publication code (faith and Bible).</summary>
        public const string MediatorPublicationCodeVODBibleCreation = "VODBibleCreation";

        /// <summary>Mediator publication code (family).</summary>
        public const string MediatorPublicationCodeFamilyChallenges = "FamilyChallenges";

        /// <summary>Mediator publication code (family).</summary>
        public const string MediatorPublicationCodeFamilyDatingMarriage = "FamilyDatingMarriage";

        /// <summary>Mediator publication code (children).</summary>
        public const string MediatorPublicationCodeBJF = "BJF";

        /// <summary>Mediator publication code (series / music-flag).</summary>
        public const string MediatorPublicationCodeSeriesBJFSongs = "SeriesBJFSongs";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesTranslation = "VODActivitiesTranslation";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesAVProduction = "VODActivitiesAVProduction";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesPrintingShipping = "VODActivitiesPrintingShipping";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesConstruction = "VODActivitiesConstruction";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesReliefWork = "VODActivitiesReliefWork";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesTheoSchools = "VODActivitiesTheoSchools";

        /// <summary>Mediator publication code (activities).</summary>
        public const string MediatorPublicationCodeVODActivitiesSpecialEvents = "VODActivitiesSpecialEvents";

        /// <summary>Mediator publication code (meetings and ministry).</summary>
        public const string MediatorPublicationCodeVODMinistryTools = "VODMinistryTools";

        /// <summary>Mediator publication code (meetings and ministry).</summary>
        public const string MediatorPublicationCodeVODMinistryImproveSkills = "VODMinistryImproveSkills";

        /// <summary>Mediator publication code (meetings and ministry).</summary>
        public const string MediatorPublicationCodeVODMinistryMethods = "VODMinistryMethods";

        /// <summary>Mediator publication code (meetings and ministry).</summary>
        public const string MediatorPublicationCodeMeetingsConventions = "MeetingsConventions";

        /// <summary>Mediator publication code (meetings and ministry).</summary>
        public const string MediatorPublicationCodeVODSampleConversations = "VODSampleConversations";

        /// <summary>Mediator publication code (organization).</summary>
        public const string MediatorPublicationCodeReports = "Reports";

        /// <summary>Mediator publication code (organization).</summary>
        public const string MediatorPublicationCodeVODOrgBethel = "VODOrgBethel";

        /// <summary>Mediator publication code (organization).</summary>
        public const string MediatorPublicationCodeAccomplishMinistry = "AccomplishMinistry";

        /// <summary>Mediator publication code (organization).</summary>
        public const string MediatorPublicationCodeVODOrgHistory = "VODOrgHistory";

        /// <summary>Mediator publication code (organization).</summary>
        public const string MediatorPublicationCodeVODOrgLegal = "VODOrgLegal";

        /// <summary>Mediator publication code (organization).</summary>
        public const string MediatorPublicationCodeVODOrgBloodlessMedicine = "VODOrgBloodlessMedicine";

        /// <summary>Mediator publication code (interviews and experiences).</summary>
        public const string MediatorPublicationCodeVODIntExpTransformations = "VODIntExpTransformations";

        /// <summary>Mediator publication code (interviews and experiences).</summary>
        public const string MediatorPublicationCodeVODIntExpBlessings = "VODIntExpBlessings";

        /// <summary>Mediator publication code (interviews and experiences).</summary>
        public const string MediatorPublicationCodeVODIntExpEndurance = "VODIntExpEndurance";

        /// <summary>Mediator publication code (interviews and experiences).</summary>
        public const string MediatorPublicationCodeVODIntExpYouth = "VODIntExpYouth";

        /// <summary>Mediator publication code (interviews and experiences).</summary>
        public const string MediatorPublicationCodeOriginsLife = "OriginsLife";

        /// <summary>Mediator publication code (interviews and experiences).</summary>
        public const string MediatorPublicationCodeVODIntExpArchives = "VODIntExpArchives";

        /// <summary>Mediator publication code (music).</summary>
        public const string MediatorPublicationCodeVODConvMusic = "VODConvMusic";

        /// <summary>Mediator publication code (music).</summary>
        public const string MediatorPublicationCodeMakingMusic = "MakingMusic";

        /// <summary>Mediator publication code (music).</summary>
        public const string MediatorPublicationCodeVODSingToJah = "VODSingToJah";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesBibleTeachings = "SeriesBibleTeachings";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesHappyMarriage = "SeriesHappyMarriage";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesImitateFaith = "SeriesImitateFaith";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesIronSharpens = "SeriesIronSharpens";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesJehovahsFriends = "SeriesJehovahsFriends";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesLearnFromThem = "SeriesLearnFromThem";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesWTLessons = "SeriesWTLessons";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeVODLovePeople = "VODLovePeople";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesMyTeenLife = "SeriesMyTeenLife";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesNeetaJade = "SeriesNeetaJade";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesOrgAccomplishments = "SeriesOrgAccomplishments";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesOurHistory = "SeriesOurHistory";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeVODPureWorshipIntro = "VODPureWorshipIntro";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesBibleChangesLives = "SeriesBibleChangesLives";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesGoodNews = "SeriesGoodNews";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesTruthTransforms = "SeriesTruthTransforms";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesOriginsLife = "SeriesOriginsLife";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesWCGVideos = "SeriesWCGVideos";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesWasItDesigned = "SeriesWasItDesigned";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesWhereAreTheyNow = "SeriesWhereAreTheyNow";

        /// <summary>Mediator publication code (series).</summary>
        public const string MediatorPublicationCodeSeriesWhiteboard = "SeriesWhiteboard";

        /// <summary>Normalized (lowercase) publication code token for API/display matching (see ToLowerInvariant on canonical codes).</summary>
        public const string NormalizedPublicationCodeDramasGoodNews = "dramasgoodnews";

        /// <summary>Normalized (lowercase) publication code token for API/display matching.</summary>
        public const string NormalizedPublicationCodeVODMoviesBibleTimes = "vodmoviesbibletimes";

        /// <summary>Normalized (lowercase) publication code token for API/display matching.</summary>
        public const string NormalizedPublicationCodeVODMoviesModernDay = "vodmoviesmodernday";

        /// <summary>Normalized (lowercase) publication code token for API/display matching.</summary>
        public const string NormalizedPublicationCodeVODMoviesAnimated = "vodmoviesanimated";

        /// <summary>Normalized (lowercase) publication code token for API/display matching.</summary>
        public const string NormalizedPublicationCodeVODMoviesExtras = "vodmoviesextras";

        /// <summary>Normalized (lowercase) publication code token for API/display matching.</summary>
        public const string NormalizedPublicationCodeSeriesDigForTreasures = "seriesdigfortreasures";

        /// <summary>Normalized (lowercase) publication code token for API/display matching.</summary>
        public const string NormalizedPublicationCodeSeriesBJFLessons = "seriesbjflessons";

        /// <summary>Normalized (lowercase) publication code token for Enjoy Life Forever video pubs.</summary>
        public const string NormalizedPublicationCodeVodLffVideosAd = "vodlffvideosad";

        /// <summary>Normalized (lowercase) publication code token for Enjoy Life Forever video pubs.</summary>
        public const string NormalizedPublicationCodeBodLffVideosAd = "bodlffvideosad";

        /// <summary>Fallback display label when API has no name (Enjoy Life Forever video publications).</summary>
        public const string PublicationDisplayNameEnjoyLifeForeverVideos = "Enjoy Life Forever!—Videos";

        /// <summary>Fallback display label when API has no name (Dig for Treasures series).</summary>
        public const string PublicationDisplayNameDigForTreasuresInGodsWord = "Dig for Treasures in God's Word";

        /// <summary>Fallback display label when API has no name (Become Jehovah's Friend lessons).</summary>
        public const string PublicationDisplayNameBibleStoriesForLittleOnes = "Bible Stories for Little Ones";

        /// <summary>JW GETPUB vocal music publication codes (flat MP3; seeding / catalog).</summary>
        public static readonly string[] VocalMusicCatalogPublicationCodes =
        {
            MusicPublicationCodeOsg,
            MusicPublicationCodeSjjc,
            MusicPublicationCodeSjji,
            MusicPublicationCodeSnv,
            MusicPublicationCodePksjj
        };

        /// <summary>JW GETPUB flat MP3 publication codes (Books category).</summary>
        public static readonly string[] FlatMp3BooksPublicationCodes =
        {
            "wcg", "lff", "rr", "lvs", "lfb", "bhs", "jy", "kr", "ia", "mb", "jr", "bt", "lv", "cf", "jd", "bh", "my", "lr", "cl", "fy", "gt"
        };

        /// <summary>JW GETPUB flat MP3 yearbook publication codes.</summary>
        public static readonly string[] FlatMp3YearbooksPublicationCodes =
        {
            "yb17", "yb16", "yb15", "yb14", "yb13", "yb12", "yb11", "yb10"
        };

        /// <summary>JW GETPUB flat MP3 brochure and booklet publication codes.</summary>
        public static readonly string[] FlatMp3BrochuresPublicationCodes =
        {
            "lmd", "wfg", "lffi", "th", "rj", "ypq", "hf", "jl", "yc", "hl", "fg", "ll", "lc", "lf", "la", "we"
        };

        /// <summary>
        /// JW GETPUB flat MP3 article series: More Topics (mrt), How Your Donations Are Used (hdu), Life Stories (lfs).
        /// </summary>
        public static readonly string[] FlatMp3ArticleSeriesPublicationCodes =
        {
            "mrt",
            "hdu",
            "lfs"
        };

        /// <summary>JW GETPUB pub= codes that use issue= (YYYYMM) instead of track=.</summary>
        public static readonly string[] GetPubIssueParameterPublicationCodes =
        {
            "mwbv", "jwb", "jwbrd", "jwbiv", "jwbcov", "jwbam", "jwbur", "jwbls", "jwbgg"
        };

        /// <summary>JW GETPUB pub= single-track codes (API returns files when no track param is sent).</summary>
        public static readonly string[] GetPubSingleTrackNoParamPublicationCodes =
        {
            "ivdd",
            "ivno"
        };

        /// <summary>JW GETPUB pub= single-track codes (API returns files when track=0).</summary>
        public static readonly string[] GetPubSingleTrackZeroPublicationCodes =
        {
            "bhat"
        };

        /// <summary>
        /// Text direction constant for left-to-right languages
        /// </summary>
        public const string TextDirectionLeftToRight = "ltr";

        /// <summary>
        /// Text direction constant for right-to-left languages
        /// </summary>
        public const string TextDirectionRightToLeft = "rtl";
    }

    /// <summary>
    /// Platform-specific configuration constants
    /// </summary>
    public static class Platform
    {
        /// <summary>
        /// Android platform identifier
        /// </summary>
        public const string Android = "Android";

        /// <summary>
        /// iOS platform identifier
        /// </summary>
        public const string IOs = "iOS";

        /// <summary>
        /// Windows platform identifier
        /// </summary>
        public const string Windows = "Windows";
    }

    /// <summary>Windows App SDK / WinUI integration identifiers.</summary>
    public static class WinUi
    {
        /// <summary><c>AppInstance.FindOrRegisterForKey</c> value for single-instance activation routing.</summary>
        public const string SingleInstanceRegistrationKey = "BibleAlarmInstance";
    }
}
