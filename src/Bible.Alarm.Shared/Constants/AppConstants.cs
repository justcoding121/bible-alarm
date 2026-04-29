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
        /// <summary>
        /// Primary JW.org index service base URL for GETPUBMEDIALINKS (flat video/music, drama track lookup).
        /// </summary>
        public const string JwOrgIndexServiceBaseUrl = "https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS";

        /// <summary>
        /// Redundant base URLs for GETPUBMEDIALINKS (app. and b. are equivalent). Use for retry when a fetch fails.
        /// </summary>
        public static readonly string[] JwOrgIndexServiceBaseUrls =
        {
            "https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS",
            "https://app.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS"
        };

        /// <summary>
        /// Primary JW.org Mediator API base URL for category-based content (dramas, etc.).
        /// </summary>
        public const string JwOrgMediatorApiBaseUrl = "https://app.jw-cdn.org/apis/mediator/v1";

        /// <summary>
        /// Redundant base URLs for Mediator API (b. and app. are equivalent). Use for random pick or retry.
        /// </summary>
        public static readonly string[] JwOrgMediatorApiBaseUrls =
        {
            "https://b.jw-cdn.org/apis/mediator/v1",
            "https://app.jw-cdn.org/apis/mediator/v1"
        };

        /// <summary>
        /// Media index file name prefix for new format (v2+)
        /// Old format files don't have this prefix and will be preserved for backward compatibility
        /// </summary>
        public const string MediaIndexFileNamePrefix = "v2-";
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

        /// <summary>
        /// Media index database filename
        /// </summary>
        public const string MediaIndexDatabaseFileName = "mediaIndex.db";

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

        /// <summary>
        /// Log file name pattern (without extension - Serilog will add date and extension)
        /// </summary>
        public const string LogFileNamePattern = "bible-alarm-";

        /// <summary>
        /// Media index zip file name
        /// </summary>
        public const string MediaIndexZipFileName = "index.zip";

        /// <summary>
        /// Temporary extraction directory name
        /// </summary>
        public const string TempExtractionDirectoryName = "tmp";

        /// <summary>
        /// Silent MP3 filename for Android dummy queue (ID3 e.g. title "Bible Alarm", artist "Preparing...").
        /// Change this when updating the file so bootstrap copies the new file on existing installs.
        /// </summary>
        public const string SilentMp3FileName = "silent_preparing_v2.mp3";
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

        /// <summary>
        /// Bible publication category code for vocal and instrumental music (JW catalog).
        /// </summary>
        public const string BiblePublicationCategoryMusic = "Music";

        /// <summary>
        /// Bible publication category code for scripture (JW catalog).
        /// </summary>
        public const string BiblePublicationCategoryBible = "Bible";

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
}
