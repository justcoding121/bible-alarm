#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Tests;

public sealed class AppConstantsTests
{
    /// <summary>
    /// Loads <see cref="AppConstants.Media"/> static readonly catalogs so their field initializers
    /// are executed under coverage (GETPUB / flat MP3 helpers depend on these lists).
    /// </summary>
    [Fact]
    public void Media_static_readonly_catalog_arrays_are_initialized()
    {
        Assert.NotEmpty(AppConstants.Media.ApiMisleadingGoodNewsVideoPublicationNamePhrases);
        Assert.NotEmpty(AppConstants.Media.VocalMusicCatalogPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3BooksPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3YearbooksPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3BrochuresPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.FlatMp3ArticleSeriesPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.GetPubIssueParameterPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.GetPubSingleTrackNoParamPublicationCodes);
        Assert.NotEmpty(AppConstants.Media.GetPubSingleTrackZeroPublicationCodes);
    }

    [Fact]
    public void ApiEndpoints_mirror_hosts_and_GETPUBMediator_base_urls_are_well_formed()
    {
        Assert.Equal("b.jw-cdn.org", AppConstants.ApiEndpoints.JwCdnHostB);
        Assert.Equal("app.jw-cdn.org", AppConstants.ApiEndpoints.JwCdnHostApp);

        foreach (var origin in new[] { AppConstants.ApiEndpoints.JwCdnOriginHttpsB, AppConstants.ApiEndpoints.JwCdnOriginHttpsApp })
        {
            Assert.StartsWith("https://", origin, StringComparison.Ordinal);
        }

        Assert.Equal(
            AppConstants.ApiEndpoints.JwCdnOriginHttpsB + AppConstants.ApiEndpoints.PubMediaApisGetPubMedialinksPath,
            AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl);

        Assert.Equal(2, AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls.Length);
        Assert.All(
            AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls,
            u => Assert.Contains("GETPUBMEDIALINKS", u, StringComparison.OrdinalIgnoreCase));

        Assert.EndsWith(
            AppConstants.ApiEndpoints.MediatorApisV1Path,
            AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl,
            StringComparison.Ordinal);

        Assert.Equal(2, AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls.Length);
        Assert.All(
            AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls,
            u => Assert.Contains("/apis/mediator/v1", u, StringComparison.Ordinal));

        Assert.Equal(
            AppConstants.ApiEndpoints.JwOrgPublicWebsiteHttpsOrigin + AppConstants.ApiEndpoints.JwOrgLanguagesListPath,
            AppConstants.ApiEndpoints.JwOrgLanguagesListUrl);
    }

    [Fact]
    public void ApiEndpoints_media_index_artifact_names_mediator_category_prefix_and_bundle_folders()
    {
        Assert.Equal("v2-", AppConstants.ApiEndpoints.MediaIndexFileNamePrefix);

        Assert.Equal("languages.json", AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);
        Assert.Equal("publications.json", AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);
        Assert.Equal("sections.json", AppConstants.ApiEndpoints.MediaIndexSectionsFileName);
        Assert.Equal("tracks.json", AppConstants.ApiEndpoints.MediaIndexTracksFileName);

        Assert.Equal("disc.json", AppConstants.ApiEndpoints.MediaIndexMelodyDiscInfoFileName);
        Assert.Equal("episodes.json", AppConstants.ApiEndpoints.MediaIndexVideoEpisodesFileName);
        Assert.Equal("language-discovery.json", AppConstants.ApiEndpoints.MediaIndexLanguageDiscoveryFileName);

        Assert.Equal("/categories", AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix);

        Assert.Equal("Audio", AppConstants.ApiEndpoints.MediaIndexFolderAudio);
        Assert.Equal("Melodies", AppConstants.ApiEndpoints.MediaIndexFolderMelodies);
        Assert.Equal("Vocals", AppConstants.ApiEndpoints.MediaIndexFolderVocals);
    }

    [Fact]
    public void Database_sqlite_auxiliary_suffix_list_and_schedule_media_formats()
    {
        Assert.Equal(
            new[]
            {
                AppConstants.Database.SqliteWalFileSuffix,
                AppConstants.Database.SqliteShmFileSuffix,
                AppConstants.Database.SqliteJournalFileSuffix,
            },
            AppConstants.Database.SqliteAuxiliaryFileSuffixes);

        Assert.Equal("Filename={0}", AppConstants.Database.ScheduleDatabaseConnectionStringFormat);
        Assert.Equal("Filename={0}", AppConstants.Database.MediaIndexDatabaseConnectionStringFormat);
        Assert.Equal("schedule.db", AppConstants.Database.ScheduleDatabaseFileName);
        Assert.Equal("mediaIndex.db", AppConstants.Database.MediaIndexDatabaseFileName);
    }

    [Fact]
    public void Database_legacy_schedule_filenames_media_index_upgrade_rename_suffix_contract()
    {
        Assert.Equal("bibleAlarm.db", AppConstants.Database.ScheduleDatabaseLegacyBibleAlarmFileName);
        Assert.Equal("bibleAlarm2.db", AppConstants.Database.ScheduleDatabaseLegacyBibleAlarm2FileName);

        Assert.Equal("_old", AppConstants.Database.MediaIndexDatabaseRenamedSuffix);
        Assert.Equal(
            "mediaIndex_old.db",
            AppConstants.Database.MediaIndexDatabaseFileName.Replace(
                ".db",
                AppConstants.Database.MediaIndexDatabaseRenamedSuffix + ".db",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ReviewSettings_gate_values_match_product_expectations()
    {
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumAppOpens);
        Assert.Equal(10, AppConstants.ReviewSettings.MinimumDismissals);
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumDaysSinceFirstDismissal);
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumDaysSinceInstall);
        Assert.Equal(7, AppConstants.ReviewSettings.MinimumDaysSinceFirstOpen);
        Assert.Equal(30, AppConstants.ReviewSettings.RetryAfterDaysWhenNotFinalized);
        Assert.Equal(5, AppConstants.ReviewSettings.MinimumMinutesBetweenCountedAppOpens);
    }

    [Fact]
    public void FilePaths_storage_catalog_and_cataloger_segments_match_layout_contract()
    {
        Assert.Equal("MediaCache", AppConstants.FilePaths.MediaCacheDirectoryName);
        Assert.Equal("logs", AppConstants.FilePaths.LogsDirectoryName);
        Assert.Equal("Bible.Alarm", AppConstants.FilePaths.WindowsAppDataFolderName);
        Assert.Equal("Cache", AppConstants.FilePaths.WindowsAppDataCacheFolderName);

        Assert.Equal("bootstrap.txt", AppConstants.FilePaths.BootstrapDiagnosticLogFileName);
        Assert.Equal("version.dat", AppConstants.FilePaths.MediaIndexVersionLegacyFileName);
        Assert.StartsWith("bible-alarm-", AppConstants.FilePaths.LogFileNamePattern, StringComparison.Ordinal);
        Assert.Equal("index.zip", AppConstants.FilePaths.MediaIndexZipFileName);

        Assert.Equal("media", AppConstants.FilePaths.MediaIndexCatalogRootMediaSegment);
        Assert.Equal("db", AppConstants.FilePaths.MediaIndexCatalogOutputDbDirectoryName);
        Assert.Equal("last_run_failed.txt", AppConstants.FilePaths.CatalogerLastRunFailedListFileName);
        Assert.Equal("bible_alarm_last_run_failed.txt", AppConstants.FilePaths.CatalogerTempFailedListFallbackFileName);
        Assert.Equal("tmp", AppConstants.FilePaths.TempExtractionDirectoryName);

        Assert.Equal("silent_preparing_v2.mp3", AppConstants.FilePaths.SilentMp3FileName);
        Assert.Equal("silent.mp3", AppConstants.FilePaths.SilentMp3LegacyFileName);
        Assert.Equal("silent_preparing.mp3", AppConstants.FilePaths.SilentMp3LegacyPreparingFileName);
    }

    [Fact]
    public void Fonts_bundle_files_have_distinct_aliases()
    {
        Assert.Equal("fa_solid_900.otf", AppConstants.Fonts.FontAwesomeSolidFontFileName);
        Assert.Equal("fa_regular_400.otf", AppConstants.Fonts.FontAwesomeRegularFontFileName);
        Assert.Equal("fa_brands_400.otf", AppConstants.Fonts.FontAwesomeBrandsFontFileName);

        Assert.Equal("FontAwesomeSolid", AppConstants.Fonts.FontAwesomeSolidAlias);
        Assert.Equal("FontAwesomeRegular", AppConstants.Fonts.FontAwesomeRegularAlias);
        Assert.Equal("FontAwesomeBrands", AppConstants.Fonts.FontAwesomeBrandsAlias);

        Assert.NotEqual(
            AppConstants.Fonts.FontAwesomeSolidAlias,
            AppConstants.Fonts.FontAwesomeRegularAlias);
        Assert.False(
            AppConstants.Fonts.FontAwesomeBrandsFontFileName.Equals(AppConstants.Fonts.FontAwesomeSolidFontFileName, StringComparison.Ordinal));
    }

    [Fact]
    public void AppSettings_and_CacheSettings_product_defaults()
    {
        Assert.Equal("Bible-Alarm", AppConstants.AppSettings.ApplicationName);
        Assert.Equal("Bible Alarm", AppConstants.AppSettings.ApplicationDisplayName);

        Assert.Equal(7, AppConstants.CacheSettings.MediaIndexUpdateCheckDays);
        Assert.Equal(3, AppConstants.CacheSettings.DownloadRetryAttempts);
        Assert.Equal(3, AppConstants.CacheSettings.FileExistsCheckRetryAttempts);
        Assert.Equal(30, AppConstants.CacheSettings.DownloadTimeoutSeconds);
        Assert.Equal(7, AppConstants.CacheSettings.LogFileRetentionDays);
    }
}
