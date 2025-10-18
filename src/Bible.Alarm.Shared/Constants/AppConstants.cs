namespace Bible.Alarm.Shared.Constants;

/// <summary>
/// Application constants containing sensitive information and configuration values.
/// This file centralizes all URLs, API endpoints, and other sensitive information
/// to make them easier to manage and secure.
/// </summary>
public static class AppConstants
{
    #region API Endpoints and URLs

    /// <summary>
    /// JW.org API endpoints for media content
    /// </summary>
    public static class ApiEndpoints
    {
        /// <summary>
        /// Primary JW.org index service base URL
        /// </summary>
        public const string JwOrgIndexServiceBaseUrl = "https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS";
        
        /// <summary>
        /// Alternative JW.org index service URL
        /// </summary>
        public const string JwOrgAlternativeIndexServiceUrl = "https://apps.jw.org/GETPUBMEDIALINKS";
        
        /// <summary>
        /// Media index download base URL
        /// </summary>
        public const string MediaIndexDownloadBaseUrl = "https://jthomas.info/bible-alarm/media-index";
    }

    #endregion

    #region Database Configuration

    /// <summary>
    /// Database configuration constants
    /// </summary>
    public static class Database
    {
        /// <summary>
        /// Schedule database filename
        /// </summary>
        public const string ScheduleDatabaseFileName = "bibleAlarm.db";
        
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

    #endregion

    #region File System Paths

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
        /// Log file name pattern
        /// </summary>
        public const string LogFileNamePattern = "bible-alarm-.log";
        
        /// <summary>
        /// Media index zip file name
        /// </summary>
        public const string MediaIndexZipFileName = "index.zip";
        
        /// <summary>
        /// Temporary extraction directory name
        /// </summary>
        public const string TempExtractionDirectoryName = "tmp";
    }

    #endregion

    #region Application Settings

    /// <summary>
    /// Application settings and configuration constants
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// Application name for logging
        /// </summary>
        public const string ApplicationName = "Bible-Alarm";
        
        /// <summary>
        /// Default font family name
        /// </summary>
        public const string DefaultFontFamily = "OpenSans-Regular";
        
        /// <summary>
        /// Default font file name
        /// </summary>
        public const string DefaultFontFileName = "OpenSans-Regular.ttf";
        
        /// <summary>
        /// Default font resource name
        /// </summary>
        public const string DefaultFontResourceName = "OpenSansRegular";
    }

    #endregion

    #region Cache and Download Settings

    /// <summary>
    /// Cache and download configuration constants
    /// </summary>
    public static class CacheSettings
    {
        /// <summary>
        /// Media index update check interval in hours
        /// </summary>
        public const int MediaIndexUpdateCheckHours = 12;
        
        /// <summary>
        /// Download retry attempts
        /// </summary>
        public const int DownloadRetryAttempts = 3;
        
        /// <summary>
        /// File exists check retry attempts
        /// </summary>
        public const int FileExistsCheckRetryAttempts = 3;
        
        /// <summary>
        /// Download timeout in seconds
        /// </summary>
        public const int DownloadTimeoutSeconds = 3;
        
        /// <summary>
        /// Log file retention limit in days
        /// </summary>
        public const int LogFileRetentionDays = 7;
    }

    #endregion

    #region General Settings Keys

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
        /// Key for Android battery optimization exclusion prompt shown flag
        /// </summary>
        public const string AndroidBatteryOptimizationExclusionPromptShown = "AndroidBatteryOptimizationExclusionPromptShown";
    }

    #endregion

    #region Logging Configuration

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

    #endregion

    #region Media Configuration

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
    }

    #endregion

    #region Platform-Specific Settings

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
        public const string iOS = "iOS";
        
        /// <summary>
        /// Windows platform identifier
        /// </summary>
        public const string Windows = "Windows";
    }

    #endregion
}
