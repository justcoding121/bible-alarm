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

        /// <summary>HTTP User-Agent for cataloger tool downloads from JW/CDN.</summary>
        public const string CatalogerHttpUserAgent = "Mozilla/5.0 (compatible; curl/8.0.1)";

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
