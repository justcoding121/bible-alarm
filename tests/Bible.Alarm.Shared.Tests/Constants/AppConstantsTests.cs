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

        Assert.Equal("https://www.jw.org", AppConstants.ApiEndpoints.JwOrgPublicWebsiteHttpsOrigin);

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
    public void ApiEndpoints_https_origins_paths_composed_bases_and_redundant_retry_url_arrays_contract()
    {
        Assert.Equal("https://b.jw-cdn.org", AppConstants.ApiEndpoints.JwCdnOriginHttpsB);
        Assert.Equal("https://app.jw-cdn.org", AppConstants.ApiEndpoints.JwCdnOriginHttpsApp);

        Assert.Equal("/apis/pub-media/GETPUBMEDIALINKS", AppConstants.ApiEndpoints.PubMediaApisGetPubMedialinksPath);
        Assert.Equal("/apis/mediator/v1", AppConstants.ApiEndpoints.MediatorApisV1Path);
        Assert.Equal("/en/languages", AppConstants.ApiEndpoints.JwOrgLanguagesListPath);

        Assert.Equal(
            "https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS",
            AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl);

        Assert.Equal(
            new[]
            {
                "https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS",
                "https://app.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS",
            },
            AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls);

        Assert.Equal(
            "https://app.jw-cdn.org/apis/mediator/v1",
            AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl);

        Assert.Equal(
            new[]
            {
                "https://b.jw-cdn.org/apis/mediator/v1",
                "https://app.jw-cdn.org/apis/mediator/v1",
            },
            AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrls);

        Assert.Equal(
            "https://www.jw.org/en/languages",
            AppConstants.ApiEndpoints.JwOrgLanguagesListUrl);
    }

    [Fact]
    public void Database_sqlite_auxiliary_suffix_list_and_schedule_media_formats()
    {
        Assert.Equal("-wal", AppConstants.Database.SqliteWalFileSuffix);
        Assert.Equal("-shm", AppConstants.Database.SqliteShmFileSuffix);
        Assert.Equal("-journal", AppConstants.Database.SqliteJournalFileSuffix);

        Assert.Equal(
            new[] { "-wal", "-shm", "-journal" },
            AppConstants.Database.SqliteAuxiliaryFileSuffixes);

        Assert.Equal("Filename={0}", AppConstants.Database.ScheduleDatabaseConnectionStringFormat);
        Assert.Equal("Filename={0}", AppConstants.Database.MediaIndexDatabaseConnectionStringFormat);
        Assert.Equal("Filename=/data/schedule.db", string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, "/data/schedule.db"));
        Assert.Equal(
            string.Format(AppConstants.Database.ScheduleDatabaseConnectionStringFormat, "mediaIndex.db"),
            string.Format(AppConstants.Database.MediaIndexDatabaseConnectionStringFormat, "mediaIndex.db"));
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
    public void GeneralSettingsKeys_preferences_keys_are_non_empty_stable_and_unique()
    {
        var keys = new[]
        {
            AppConstants.GeneralSettingsKeys.AlarmSeeded,
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId,
            AppConstants.GeneralSettingsKeys.ReviewRequested,
            AppConstants.GeneralSettingsKeys.DismissCount,
            AppConstants.GeneralSettingsKeys.FirstDismissalDate,
            AppConstants.GeneralSettingsKeys.AppInstallDate,
            AppConstants.GeneralSettingsKeys.ReviewAppOpenCount,
            AppConstants.GeneralSettingsKeys.ReviewFirstOpenDate,
            AppConstants.GeneralSettingsKeys.ReviewLastCountedAppOpenAtUtc,
            AppConstants.GeneralSettingsKeys.ReviewAttemptCount,
            AppConstants.GeneralSettingsKeys.ReviewLastAttemptAtUtc,
            AppConstants.GeneralSettingsKeys.ReviewLastEligibleAtUtc,
            AppConstants.GeneralSettingsKeys.ReviewCompletedOrFinalized,
            AppConstants.GeneralSettingsKeys.ReviewStateMigrated,
            AppConstants.GeneralSettingsKeys.MediaIndexVersion,
            AppConstants.GeneralSettingsKeys.AndroidBatteryOptimizationExclusionPromptShown,
        };

        Assert.All(keys, static k => Assert.False(string.IsNullOrWhiteSpace(k)));
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());

        Assert.Equal("AlarmSeeded", AppConstants.GeneralSettingsKeys.AlarmSeeded);
        Assert.Equal("LastPlayedScheduleId", AppConstants.GeneralSettingsKeys.LastPlayedScheduleId);
        Assert.Equal("ReviewRequested", AppConstants.GeneralSettingsKeys.ReviewRequested);
        Assert.Equal("DismissCount", AppConstants.GeneralSettingsKeys.DismissCount);
        Assert.Equal("FirstDismissalDate", AppConstants.GeneralSettingsKeys.FirstDismissalDate);
        Assert.Equal("AppInstallDate", AppConstants.GeneralSettingsKeys.AppInstallDate);
        Assert.Equal("ReviewAppOpenCount", AppConstants.GeneralSettingsKeys.ReviewAppOpenCount);
        Assert.Equal("ReviewFirstOpenDate", AppConstants.GeneralSettingsKeys.ReviewFirstOpenDate);
        Assert.Equal("ReviewLastCountedAppOpenAtUtc", AppConstants.GeneralSettingsKeys.ReviewLastCountedAppOpenAtUtc);
        Assert.Equal("ReviewAttemptCount", AppConstants.GeneralSettingsKeys.ReviewAttemptCount);
        Assert.Equal("ReviewLastAttemptAtUtc", AppConstants.GeneralSettingsKeys.ReviewLastAttemptAtUtc);
        Assert.Equal("ReviewLastEligibleAtUtc", AppConstants.GeneralSettingsKeys.ReviewLastEligibleAtUtc);
        Assert.Equal("ReviewCompletedOrFinalized", AppConstants.GeneralSettingsKeys.ReviewCompletedOrFinalized);
        Assert.Equal("ReviewStateMigrated", AppConstants.GeneralSettingsKeys.ReviewStateMigrated);
        Assert.Equal("MediaIndexVersion", AppConstants.GeneralSettingsKeys.MediaIndexVersion);
        Assert.Equal(
            "AndroidBatteryOptimizationExclusionPromptShown",
            AppConstants.GeneralSettingsKeys.AndroidBatteryOptimizationExclusionPromptShown);
    }

    [Fact]
    public void Logging_serilog_templates_environment_and_fallback_tokens()
    {
        Assert.Equal(
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            AppConstants.Logging.ConsoleOutputTemplate);

        Assert.Equal(
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            AppConstants.Logging.FileOutputTemplate);

        Assert.Equal("DEBUG", AppConstants.Logging.DebugEnvironment);
        Assert.Equal("AssemblyVersionNotFound", AppConstants.Logging.AssemblyVersionFallback);
        Assert.Equal("Unknown error", AppConstants.Logging.UnknownErrorFallback);

        Assert.Equal(
            "An error happened when ringing the alarm.",
            AppConstants.Logging.AlarmDiagnostics.RingingAlarmFailed);
        Assert.Equal(
            "An error happened when creating the task to ring the alarm.",
            AppConstants.Logging.AlarmDiagnostics.CreatingAlarmRingTaskFailed);
        Assert.Equal(
            "An error happened when playing alarm.",
            AppConstants.Logging.AlarmDiagnostics.PlayingAlarmFailed);
        Assert.Equal(
            "An error happened when starting user-initiated playback.",
            AppConstants.Logging.AlarmDiagnostics.StartingUserInitiatedPlaybackFailed);
        Assert.Equal(
            "An error happened when review was requested.",
            AppConstants.Logging.AlarmDiagnostics.ReviewRequestedFailed);
    }

    [Fact]
    public void Logging_process_schedule_lookup_exact_alarm_disposal_enable_and_state_templates()
    {
        Assert.Equal("Unobserved task exception.", AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        Assert.Equal(
            "Unobserved task exception in SchedulerJob",
            AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskExceptionInSchedulerJob);
        Assert.Equal(
            "Error stopping current playback before handling alarm for schedule {ScheduleId}",
            AppConstants.Logging.ProcessDiagnosticsLog.ErrorStoppingPlaybackBeforeAlarmForSchedule);
        Assert.Equal(
            "Unhandled exception. IsTerminating: {IsTerminating}",
            AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionIsTerminating);
        Assert.Equal(
            "Unhandled exception (non-Exception object): {ExceptionObject}. IsTerminating: {IsTerminating}",
            AppConstants.Logging.ProcessDiagnosticsLog.UnhandledNonExceptionObjectIsTerminating);
        Assert.Equal(
            "Unhandled exception in SchedulerJob",
            AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionInSchedulerJob);
        Assert.Equal(
            "Alarm triggered for schedule {ScheduleId} while playback is active - stopping current playback to handle new alarm",
            AppConstants.Logging.ProcessDiagnosticsLog.AlarmTriggeredWhilePlaybackActiveStoppingForNewAlarm);
        Assert.Equal(
            "Managed exception marshaling to ObjC (Mode={Mode})",
            AppConstants.Logging.ProcessDiagnosticsLog.ManagedExceptionMarshalingToObjCMode);
        Assert.Equal(
            "ObjC exception caught (Mode={Mode}, Exception={Exception})",
            AppConstants.Logging.ProcessDiagnosticsLog.ObjCExceptionCaughtModeException);
        Assert.Equal(
            "Alarm triggered for schedule {ScheduleId} with tap disabled - using foreground service notification (no sound)",
            AppConstants.Logging.ProcessDiagnosticsLog.AlarmTriggeredTapDisabledForegroundNoSound);
        Assert.Equal(
            "Fetch failed during modal appearing",
            AppConstants.Logging.ProcessDiagnosticsLog.FetchFailedDuringModalAppearing);
        Assert.Equal(
            "Error happened when playing alarm manually.",
            AppConstants.Logging.ProcessDiagnosticsLog.ErrorPlayingAlarmManually);

        Assert.Equal(
            "Schedule {ScheduleId} not found - stopping foreground service",
            AppConstants.Logging.ScheduleLookupDiagnosticsLog.NotFoundStoppingForegroundService);
        Assert.Equal(
            "Schedule {ScheduleId} not found for deletion",
            AppConstants.Logging.ScheduleLookupDiagnosticsLog.NotFoundForDeletion);
        Assert.Equal(
            "LoadExistingScheduleAsync: Schedule {ScheduleId} not found in database",
            AppConstants.Logging.ScheduleLookupDiagnosticsLog.LoadExistingScheduleNotFoundInDatabase);
        Assert.Equal(
            "SetScheduleId: Schedule {ScheduleId} not found in state",
            AppConstants.Logging.ScheduleLookupDiagnosticsLog.SetScheduleIdNotFoundInState);

        Assert.Equal(
            "SecurityException when scheduling alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.",
            AppConstants.Logging.AndroidExactAlarmSchedulingLog.SecurityExceptionSchedulingAlarmForSchedule);
        Assert.Equal(
            "SecurityException when updating schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.",
            AppConstants.Logging.AndroidExactAlarmSchedulingLog.SecurityExceptionUpdatingSchedule);

        Assert.Equal(
            "Error during cancellation token source disposal",
            AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);

        Assert.Equal(
            "Cannot enable schedule {ScheduleId} with NotificationEnabled=true - notification permission denied",
            AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableNotificationDeniedTapToPlay);
        Assert.Equal(
            "Cannot enable schedule {ScheduleId} - notification permission denied. iOS requires notification permission for reminders.",
            AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableIosRemindersPermissionDenied);
        Assert.Equal(
            "Permission request timeout for schedule {ScheduleId}",
            AppConstants.Logging.ScheduleEnableDiagnosticsLog.PermissionRequestTimeoutForSchedule);

        Assert.Equal(
            "Android: Schedule {ScheduleId} has NotificationEnabled=true, checking notification permission before enabling reminder",
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidScheduleNotificationEnabledCheckingPermissionBeforeReminder);
        Assert.Equal(
            "Android: Notification permission granted for schedule {ScheduleId}",
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidNotificationPermissionGrantedForSchedule);
        Assert.Equal(
            "Android: Schedule {ScheduleId} has NotificationEnabled=false, no permission check needed",
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidScheduleNotificationDisabledNoPermissionCheckNeeded);
        Assert.Equal(
            "Requesting iOS notification permission for schedule {ScheduleId}",
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.RequestingIosNotificationPermissionForSchedule);
    }

    [Fact]
    public void Logging_playback_main_activity_battery_bootstrap_scheduler_database_seed_spot_checks()
    {
        Assert.Equal(
            "PlayScheduleAsync: PlayLock already held — skipping (schedule {ScheduleId})",
            AppConstants.Logging.SchedulePlaybackServiceDiagnosticsLog.PlayScheduleAsyncPlayLockAlreadyHeldSkipping);

        Assert.Equal(
            "PlayScheduleAsync: overall timeout ({Timeout}s) for schedule {ScheduleId}. Releasing PlayLock — background task continues.",
            AppConstants.Logging.SchedulePlaybackServiceDiagnosticsLog.PlayScheduleAsyncOverallTimeoutReleasingPlayLockBackgroundContinues);

        Assert.Equal(
            "Playback cancelled for schedule {ScheduleId}",
            AppConstants.Logging.SchedulePlaybackServiceDiagnosticsLog.PlaybackCancelledForSchedule);

        Assert.Equal(
            "Error setting up background tasks",
            AppConstants.Logging.MainActivityBackgroundTaskHelperDiagnosticsLog.ErrorSettingUpBackgroundTasks);

        Assert.Equal(
            "Error marking battery optimization modal as shown",
            AppConstants.Logging.BatteryOptimizationDiagnosticsLog.ErrorMarkingBatteryOptimizationModalAsShown);

        Assert.Equal(
            "Error checking if battery optimization modal should be shown",
            AppConstants.Logging.BatteryOptimizationDiagnosticsLog.ErrorCheckingIfBatteryOptimizationModalShouldBeShown);

        Assert.Equal(
            "IsBootstrapReady changed to {Value}",
            AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.IsBootstrapReadyChangedTo);

        Assert.Equal(
            "Bootstrap not completed yet, waiting for bootstrap before running scheduler",
            AppConstants.Logging.SchedulerDiagnosticsLog.BootstrapNotCompletedWaitingForBootstrap);

        Assert.Equal(
            "An error happenned inside cleanup task.",
            AppConstants.Logging.SchedulerDiagnosticsLog.ErrorInsideCleanupTask);

        Assert.Equal(
            "Seeded default alarm schedule. ScheduleId={ScheduleId}, Name={Name}",
            AppConstants.Logging.DatabaseSeedDiagnosticsLog.SeededDefaultAlarmSchedule);
    }

    [Fact]
    public void Logging_schedule_initialization_command_validation_and_database_version_templates()
    {
        Assert.Equal(
            "InitializeNewScheduleAsync: Mapped sample schedule. MusicLanguageCode={LanguageCode}, MusicTrackCode={TrackCode}, MusicPublicationCode={PublicationCode}, MusicSectionCode={SectionCode}",
            AppConstants.Logging.ScheduleInitializationDiagnosticsLog.InitializeNewScheduleMappedSampleMusicCodes);

        Assert.Equal(
            "LoadExistingScheduleAsync: Loading schedule {ScheduleId} from database",
            AppConstants.Logging.ScheduleInitializationDiagnosticsLog.LoadExistingScheduleLoadingFromDatabase);

        Assert.Equal(
            "CancelCommand: Cancel button clicked. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}",
            AppConstants.Logging.ScheduleCommandDiagnosticsLog.CancelCommandCancelButtonClicked);

        Assert.Equal(
            "SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveCommandSaveButtonClicked);

        Assert.Equal(
            "Validation failed: No days of week selected",
            AppConstants.Logging.ScheduleValidationServiceDiagnosticsLog.ValidationFailedNoDaysOfWeekSelected);

        Assert.Equal(
            "Schedule database version mismatch - stored: {StoredVersion}, current: {CurrentVersion}. Migration check needed.",
            AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.VersionMismatchStoredVersusCurrent);

        Assert.Equal(
            "Failed to check Schedule database version, will perform migration check",
            AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.FailedToCheckScheduleDatabaseVersionPerformingMigrationCheck);

        Assert.Equal(
            "Saved Schedule database version {Version} to Preferences",
            AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.SavedScheduleDatabaseVersionToPreferences);

        Assert.Equal(
            "Failed to save Schedule database version to Preferences",
            AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.FailedToSaveScheduleDatabaseVersionToPreferences);
    }

    [Fact]
    public void Logging_schedule_save_prep_and_music_cascade_handler_templates()
    {
        Assert.Equal(
            "PrepareModelForSave: Starting. musicUpdated={MusicUpdated}, IsNewSchedule={IsNewSchedule}",
            AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveStarting);

        Assert.Equal(
            "SaveAsync: BiblePublicationSchedule has empty PublicationCode, defaulting to 'nwt' (2013)",
            AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.SaveAsyncBiblePublicationEmptyPublicationCodeDefaultingNwt);

        Assert.Equal(
            "PrepareScheduleStateItem: Final scheduleStateItem before dispatch - MusicPublicationCode={PublicationCode}, MusicLanguageCode={LanguageCode}, MusicTrackCode={TrackCode}, MusicId={MusicId}",
            AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemFinalBeforeDispatch);

        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - CurrentSchedule is null, exiting",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncCurrentScheduleNullExiting);

        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}, MusicEnabled={MusicEnabled}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncPublicationSectionTrackMusicEnabled);

        Assert.Equal(
            "MusicCascadeHandler: Error during cascade",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ErrorDuringCascade);
    }

    [Fact]
    public void Logging_music_cascade_modal_publication_section_and_cycle_guard_templates()
    {
        Assert.Equal(
            "MusicCascadeHandler: RefreshModalCountsIfNeeded - Current: PublicationCount={CurrentPubCount}, SectionCount={CurrentSectionCount}, New: PublicationCount={NewPubCount}, SectionCount={NewSectionCount}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.RefreshModalCountsIfNeededCurrentVsNew);

        Assert.Equal(
            "MusicCascadeHandler: Modal counts unchanged, skipping dispatch",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ModalCountsUnchangedSkippingDispatch);

        Assert.Equal(
            "MusicCascadeHandler: Publication cascade - publication={PublicationCode}, language={LanguageCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationCascade);

        Assert.Equal(
            "MusicCascadeHandler: Values unchanged, skipping dispatch to prevent cycle. publication={PublicationCode}, section={SectionCode}, track={TrackCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatchCycle);
    }

    [Fact]
    public void Logging_music_cascade_handle_async_branch_messages_exact_strings()
    {
        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - No publication code, calling HandleLanguageCascadeAsync",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncNoPublicationCodeCallingLanguageCascade);

        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - Sectioned publication but no section code, calling HandlePublicationCascadeAsync",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncSectionedNoSectionCodeCallingPublicationCascade);

        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - Sectioned publication but no track code, calling HandleSectionCascadeAsync",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncSectionedNoTrackCodeCallingSectionCascade);

        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - Flat publication but no track code, calling HandleFlatPublicationCascadeAsync",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncFlatNoTrackCodeCallingFlatCascade);

        Assert.Equal(
            "MusicCascadeHandler: HandleAsync - Everything is set, calling RefreshModalCountsIfNeededAsync",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncEverythingSetCallingRefreshModalCounts);
    }

    [Fact]
    public void Logging_music_cascade_modal_refresh_flat_language_publication_section_exact_strings()
    {
        Assert.Equal(
            "MusicCascadeHandler: Refreshing modal counts. PublicationCount={PublicationCount}, SectionCount={SectionCount}, PublicationCode={PublicationCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.RefreshingModalCounts);

        Assert.Equal(
            "MusicCascadeHandler: Error refreshing modal counts",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ErrorRefreshingModalCounts);

        Assert.Equal(
            "MusicCascadeHandler: Flat publication cascade - publication={PublicationCode}, language={LanguageCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.FlatPublicationCascade);

        Assert.Equal(
            "MusicCascadeHandler: Publication not found in database: {PublicationCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationNotFoundInDatabase);

        Assert.Equal(
            "MusicCascadeHandler: No valid track found for publication={PublicationCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication);

        Assert.Equal(
            "MusicCascadeHandler: Language cascade - language={LanguageCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.LanguageCascade);

        Assert.Equal(
            "MusicCascadeHandler: Failed to catalog publication={PublicationCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.FailedToCatalogPublication);

        Assert.Equal(
            "MusicCascadeHandler: No publication found for language={LanguageCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoPublicationFoundForLanguage);

        Assert.Equal(
            "MusicCascadeHandler: Publication not found: {PublicationCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationNotFound);

        Assert.Equal(
            "MusicCascadeHandler: Section cascade - section={SectionCode}, publication={PublicationCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.SectionCascade);

        Assert.Equal(
            "MusicCascadeHandler: No tracks found for sectionCode={SectionCode}",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.NoTracksFoundForSection);
    }

    [Fact]
    public void Logging_category_selection_auto_populate_handler_exact_strings()
    {
        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Starting auto-population for category={CategoryName}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.StartingAutoPopulation);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: CurrentSchedule is null, skipping auto-population",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.CurrentScheduleNullSkippingAutoPopulation);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Preserving previous language={LanguageCode} (has publications in new category={CategoryName})",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.PreservingPreviousLanguageHasPublicationsInCategory);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Selected English language (default/fallback)",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedEnglishLanguageDefaultFallback);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: English not found, selected first available language={LanguageCode}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.EnglishNotFoundSelectedFirstAvailableLanguage);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: No languages found for category={CategoryName}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoLanguagesFoundForCategory);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Failed to catalog publication={PublicationCode} for language={LanguageCode}, trying next",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.FailedToCatalogPublicationTryingNext);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Publication={PublicationCode} for language={LanguageCode} already cataloged with first section and tracks",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.PublicationAlreadyCatalogedWithFirstSectionAndTracks);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode} (cataloged and can be queried with language={LanguageCode})",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedPublicationCatalogedCanQueryWithLanguage);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Publication={PublicationCode} cataloged but cannot be queried with language={LanguageCode} (may not have LanguageId), trying next",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.PublicationCatalogedCannotQueryWithLanguageTryingNext);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Selected publication without LanguageId={PublicationCode}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedPublicationWithoutLanguageId);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: No publication found or cataloged for language={LanguageCode}, category={CategoryName}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoPublicationFoundOrCatalogedForLanguageCategory);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Selected publication={PublicationCode}, withoutLanguage={WithoutLanguage}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedPublicationWithoutLanguageFlag);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: selectedLanguage is null but publication requires language",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SelectedLanguageNullButPublicationRequiresLanguage);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}, language={LanguageCode}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoValidTrackFoundForPublicationAndLanguage);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: No valid track found for publication={PublicationCode}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NoValidTrackFoundForPublication);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Auto-populated - Language={LanguageCode}, Publication={PublicationCode}, Section={SectionCode}, Track={TrackCode}, WithoutLanguage={WithoutLanguage}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.AutoPopulatedSummary);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Setting language to English default for publication without LanguageId={PublicationCode}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.SettingLanguageEnglishDefaultForPublicationWithoutLanguageId);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Network error during auto-population for category={CategoryName}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NetworkErrorDuringAutoPopulation);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Error during auto-population for category={CategoryName}",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.ErrorDuringAutoPopulation);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Reverting to previous schedule state after network error",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.RevertingToPreviousScheduleStateAfterNetworkError);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Reverting to previous schedule state after error",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.RevertingToPreviousScheduleStateAfterError);

        Assert.Equal(
            "CategorySelectionAutoPopulateHandler: Error checking if publication {PublicationCode} is cataloged",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.ErrorCheckingIfPublicationCataloged);
    }

    [Fact]
    public void Logging_bible_publication_cascade_handler_exact_strings()
    {
        Assert.Equal(
            "BiblePublicationCascadeHandler: Error during cascade",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ErrorDuringCascade);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Language cascade - language={LanguageCode}, category={CategoryName}, existingPublication={ExistingPublication}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.LanguageCascade);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Using existing publication={PublicationCode} from schedule",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.UsingExistingPublicationFromSchedule);

        Assert.Equal(
            "BiblePublicationCascadeHandler: No valid track found for existing publication={PublicationCode} after cataloging",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForExistingPublicationAfterCataloging);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Existing publication={PublicationCode} not available for language={LanguageCode}, selecting new publication",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ExistingPublicationNotAvailableSelectingNew);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Failed to catalog publication={PublicationCode} for language={LanguageCode}, trying next",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.FailedToCatalogPublicationTryingNext);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Publication={PublicationCode} cataloged but cannot be queried with language={LanguageCode} (may not have LanguageId), trying next",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PublicationCatalogedCannotQueryWithLanguageTryingNext);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Selected publication={PublicationCode} (cataloged and queryable for language={LanguageCode})",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SelectedPublicationCatalogedQueryableForLanguage);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Selected publication without LanguageId={PublicationCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SelectedPublicationWithoutLanguageId);

        Assert.Equal(
            "BiblePublicationCascadeHandler: No publication found for language={LanguageCode}, category={CategoryName}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoPublicationFoundForLanguageCategory);

        Assert.Equal(
            "BiblePublicationCascadeHandler: No valid track found for publication={PublicationCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFoundForPublication);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Publication cascade - publication={PublicationCode}, language={LanguageCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PublicationCascade);

        Assert.Equal(
            "BiblePublicationCascadeHandler: No valid track found",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoValidTrackFound);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Section cascade - sectionCode={SectionCode}, publication={PublicationCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.SectionCascade);

        Assert.Equal(
            "BiblePublicationCascadeHandler: No tracks found for sectionCode={SectionCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.NoTracksFoundForSectionCode);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Error getting publication modal item count. LanguageCode={LanguageCode}, CategoryName={CategoryName}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ErrorGettingPublicationModalItemCount);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Values unchanged, skipping dispatch. publication={PublicationCode}, sectionCode={SectionCode}, track={TrackCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatch);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Preserving category={CategoryName}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PreservingCategory);

        Assert.Equal(
            "BiblePublicationCascadeHandler: {Action} language for no-language publication={PublicationCode} (LanguageCode: {LanguageCode})",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ActionLanguageForNoLanguagePublication);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Preserving language={LanguageCode}",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PreservingLanguage);
    }

    [Fact]
    public void Logging_bible_publication_selection_item_selector_get_section_and_track_exact_strings()
    {
        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: Starting for publication={PublicationCode}, language={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackStarting);

        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: Found {SectionCount} sections, using sectioned flow",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackFoundSectionsSectionedFlow);

        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: Sectioned result: sectionCode={SectionCode}, trackCode={TrackCode}, sectionName={SectionName}, trackTitle={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackSectionedResult);

        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: SectionName is empty for sectionCode={SectionCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackSectionNameEmptyForSectionCode);

        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: TrackTitle is empty for trackCode={TrackCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackTrackTitleEmptyForTrackCode);

        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: No sections found, using non-sectioned flow",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackNoSectionsUsingNonSectionedFlow);

        Assert.Equal(
            "GetSectionAndTrackForPublicationAsync: Non-sectioned result: trackCode={TrackCode}, trackTitle={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackNonSectionedResult);
    }

    [Fact]
    public void Logging_bible_publication_selection_item_selector_publication_for_language_exact_strings()
    {
        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Starting for language={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionTrackLangStarting);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Failed to catalog publication={PublicationCode} for language={LanguageCode}, trying next",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.FailedToCatalogPublicationTryingNext);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: No publication found for language={LanguageCode}, category={CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.NoPublicationFoundForLanguageCategory);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Selected publication code={PublicationCode}, name={PublicationName}, withoutLanguage={WithoutLanguage}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.SelectedPublicationCodeNameWithoutLanguage);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Ensuring publication {PublicationCode} exists for language {LanguageCode} before getting sections",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.EnsuringPublicationExistsBeforeGettingSections);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Failed to ensure publication {PublicationCode} exists, continuing anyway",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.FailedToEnsurePublicationExistsContinuingAnyway);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Querying sections for publication without language={PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.QueryingSectionsPublicationWithoutLanguage);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: No sections found in database after ensuring publication exists, publication is likely non-sectioned",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.NoSectionsInDbAfterEnsuringLikelyNonSectioned);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: biblePublicationSectionService is null, using MediaService.GetBiblePublicationSections",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.BiblePublicationSectionServiceNullUsingMediaServiceSections);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Found {SectionCount} sections, using sectioned flow",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationFoundSectionsSectionedFlow);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Sectioned result: sectionCode={SectionCode}, sectionName={SectionName}, trackCode={TrackCode}, trackTitle={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionedResult);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: SectionName is empty for sectionCode={SectionCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionNameEmptyForSectionCode);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: TrackTitle is empty for trackCode={TrackCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationTrackTitleEmptyForTrackCode);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: No sections found, using non-sectioned flow",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationNoSectionsUsingNonSectionedFlow);

        Assert.Equal(
            "GetPublicationSectionAndTrackForLanguageAsync: Non-sectioned result: trackCode={TrackCode}, trackTitle={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationNonSectionedResult);
    }

    [Fact]
    public void Logging_bible_publication_selection_data_provider_exact_strings()
    {
        Assert.Equal(
            "PopulateLanguagesAsync: Loaded {LanguageCount} languages from GetBiblePublicationLanguages, currentLanguageCode={CurrentLanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulateLanguagesLoaded);

        Assert.Equal(
            "PopulateLanguagesAsync: Marked language {LanguageCode} ({LanguageName}) as selected",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulateLanguagesMarkedSelected);

        Assert.Equal(
            "PopulatePublicationsAsync: Category is null or empty. Category must always be selected. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsCategoryNullOrEmpty);

        Assert.Equal(
            "PopulatePublicationsAsync: All {ExpectedCount} expected publications already cataloged for language={LanguageCode}, category={CategoryName}, skipping fetch",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedAlreadyCatalogedSkippingFetch);

        Assert.Equal(
            "PopulatePublicationsAsync: Starting fetch with retries for language={LanguageCode}, category={CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsStartingFetchWithRetries);

        Assert.Equal(
            "PopulatePublicationsAsync: All {ExpectedCount} expected publications cataloged on attempt {Attempt} for language={LanguageCode}, category={CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedCatalogedOnAttempt);

        Assert.Equal(
            "PopulatePublicationsAsync: No progress between retries ({CatalogedCount} cataloged, {ExpectedCount} expected). Remaining placeholders are unfetchable. Stopping retries for language={LanguageCode}, category={CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsNoProgressBetweenRetriesStopping);

        Assert.Equal(
            "PopulatePublicationsAsync: Attempt {Attempt}: Still waiting for {Count} publications to be cataloged: {Placeholders}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptStillWaitingForPlaceholders);

        Assert.Equal(
            "PopulatePublicationsAsync: Attempt {Attempt}: Only {ActualCount}/{ExpectedCount} publications found, will retry",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptPartialCountWillRetry);

        Assert.Equal(
            "PopulatePublicationsAsync: Attempt {Attempt}: No publications found yet, will retry",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptNoPublicationsYetWillRetry);

        Assert.Equal(
            "PopulatePublicationsAsync: Fetch cancelled at attempt {Attempt} for language={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsFetchCancelledAtAttempt);

        Assert.Equal(
            "PopulatePublicationsAsync: Attempt {Attempt} failed for language={LanguageCode}, will retry",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAttemptFailedWillRetry);

        Assert.Equal(
            "PopulatePublicationsAsync: Timeout after {Attempts} attempts waiting for all publications to be cataloged for language {LanguageCode}. Some may still be placeholders.",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsTimeoutWaitingForCatalog);

        Assert.Equal(
            "PopulatePublicationsAsync: Final fetch attempt failed for language={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsFinalFetchAttemptFailed);

        Assert.Equal(
            "PopulatePublicationsAsync: Removed {Count} unfetchable placeholder publications: {Codes}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsRemovedUnfetchablePlaceholders);

        Assert.Equal(
            "PopulatePublicationsAsync: Sorted {Count} publications for category={Category}. First 3: {FirstThree}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsSortedFirstThreeCodes);

        Assert.Equal(
            "DispatchDefaultPublicationAsync: Starting for language={LanguageCode}, publication={PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationStarting);

        Assert.Equal(
            "DispatchDefaultPublicationAsync: No sections found for language={LanguageCode}, publication={PublicationCode}. This publication may not have section data.",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationNoSectionsMayBeFlat);

        Assert.Equal(
            "DispatchDefaultPublicationAsync: First section index={SectionIndex}, name={SectionName}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationFirstSectionIndexAndName);

        Assert.Equal(
            "DispatchDefaultPublicationAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, sectionIndex={SectionIndex}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationNoTracksForSection);

        Assert.Equal(
            "DispatchDefaultPublicationAsync: First track trackCode={TrackCode}, title={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationFirstTrackCodeAndTitle);

        Assert.Equal(
            "DispatchDefaultPublicationAsync: Dispatching TrackSelectedAction for publication={PublicationCode}, section={SectionCode}/{SectionName}, track={TrackCode}/{TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationDispatchingTrackSelected);

        Assert.Equal(
            "BibleSelectionDataProvider: Error dispatching default publication selection for language={LanguageCode}, publication={PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.ErrorDispatchingDefaultPublicationSelection);
    }

    [Fact]
    public void Logging_bible_publication_selection_section_track_resolver_exact_strings()
    {
        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: First section codeKey={SectionCodeKey}, sectionCode={SectionCode}, name={SectionName}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstSectionFirstSectionCodeKey);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: Found {TrackCount} tracks for section={SectionCode} using direct query",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FoundTracksForSectionUsingDirectQuery);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: Section found but no tracks. SectionCode={SectionCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.SectionFoundButNoTracks);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: Publication not found or has no sections. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.PublicationNotFoundOrHasNoSections);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: No tracks found in database, fetching tracks for first section...",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksInDatabaseFetchingFirstSection);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: Tracks fetched successfully, re-querying from database",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.TracksFetchedSuccessfullyRequeryingFromDatabase);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: Failed to fetch tracks for section={SectionCode}, publication={PublicationCode}, language={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FailedToFetchTracksForSection);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: Falling back to mediaService.GetBiblePublicationTracks",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FallingBackToMediaServiceGetBiblePublicationTracks);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, sectionCode={SectionCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksFoundForLanguagePublicationSection);

        Assert.Equal(
            "GetFirstSectionAndTrackFromSectionsAsync: First track trackCode={TrackCode}, title={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstSectionFirstTrackCodeAndTitle);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: Starting for language={LanguageCode}, publication={PublicationCode}, biblePublicationService={HasService}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstTrackNonSectionedStarting);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: biblePublicationService is null, returning empty result",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.BiblePublicationServiceNullReturningEmpty);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: Loaded publication={PublicationName}, TracksCount={TracksCount}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.LoadedPublicationNameAndTracksCount);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: No tracks found in database, ensuring publication exists (will fetch tracks for non-sectioned publications)...",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksEnsuringPublicationExistsFetchNonSectioned);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: Publication cataloged successfully, re-querying tracks",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.PublicationCatalogedSuccessfullyRequeryingTracks);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: Failed to catalog publication={PublicationCode} for language={LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FailedToCatalogPublicationForLanguage);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: No tracks found for publication without language={PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksFoundPublicationWithoutLanguage);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksFoundForLanguageAndPublication);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: Found first track trackCode={TrackCode}, Title={TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FoundFirstTrackCodeAndTitle);

        Assert.Equal(
            "CheckIfPublicationWithFirstSectionCatalogedAsync: Error checking if publication {PublicationCode} is cataloged",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.ErrorCheckingPublicationCataloged);
    }

    [Fact]
    public void Logging_bible_publication_selection_command_handler_templates()
    {
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionStarting);
        Assert.Contains(
            "{HasService}",
            AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateSectionSelectionStarting);

        Assert.StartsWith(
            "BibleSelectionCommandHandler:",
            AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.SelectLanguageNetworkErrorDuringSelection,
            StringComparison.Ordinal);

        var categoryBug = AppConstants.Logging.BiblePublicationSelectionCommandHandlerDiagnosticsLog.CreateBiblePublicationItemCategoryNullBug;
        Assert.Contains("{ScheduleId}", categoryBug);
        Assert.Contains("{PublicationCode}", categoryBug);
    }

    [Fact]
    public void Logging_track_selection_data_provider_and_schedule_effects_templates()
    {
        Assert.StartsWith(
            "TrackSelectionDataProvider.PopulateTracks:",
            AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLanguagePublicationSection,
            StringComparison.Ordinal);
        Assert.Contains(
            "{TrackCount}",
            AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLoadedSectionedTrackCount);
        Assert.Contains(
            "{TrackCode}",
            AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.SetSelectedTrackCouldNotFindInCollection);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleScheduleId);
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelPopulateMusicSectionNameInstrumental);
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleMusicCascadeTriggered);
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.CannotPopulateModalCountsScopeFactoryUnavailable);
    }

    [Fact]
    public void Logging_schedule_effects_crud_and_db_update_templates()
    {
        Assert.Contains(
            "{Name}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleScheduleIdName);

        Assert.Contains(
            "{LastPlayedScheduleId}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleSuccessDeletedWasNotLastPlayedNoRefreshNeeded);

        Assert.Contains(
            "{IsNull}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ScheduleDeleteHandlerHandleAsyncCalled);
        Assert.Contains(
            "{DispatcherNull}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ScheduleDeleteHandlerHandleAsyncCalled);

        Assert.Equal(
            "ScheduleDeleteHandler: HandleAsync - Action is null!",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ScheduleDeleteHandlerHandleAsyncActionIsNull);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleBeforeSavePublicationLanguage);
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleSavedToDb);

        Assert.Contains(
            "{NumberOfTracksToPlay}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.UpdateScheduleInDatabaseAsyncActionScheduleTracksAndAlwaysPlay);
        Assert.Contains(
            "{SavedPublicationCode}",
            AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelMusicPublicationMismatch);
    }

    [Fact]
    public void Logging_track_selection_sync_handler_templates()
    {
        Assert.Contains(
            "{OldLang}",
            AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedMusicLanguageChangedSyncing);
        Assert.Contains(
            "{NewLang}",
            AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedMusicLanguageChangedSyncing);

        var bibleReceived = AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedBiblePublicationReceivedAction;
        Assert.StartsWith("TrackSelectionSyncHandler:", bibleReceived, StringComparison.Ordinal);
        Assert.Contains("{PublicationCode}", bibleReceived);

        Assert.Contains(
            "{CategoryName}",
            AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.SetCategoryFromBiblePublicationStateItemWasNull);
    }

    [Fact]
    public void Logging_track_selection_sync_music_dispatch_and_vocal_language_templates()
    {
        var bibleDispatch = AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedBiblePublicationDispatchingUpdate;
        Assert.Contains("{ScheduleId}", bibleDispatch);
        Assert.Contains("{TrackCode}", bibleDispatch);
        Assert.Contains("{SectionCode}", bibleDispatch);

        var musicReceived = AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedReceivedActionMusic;
        Assert.StartsWith("ScheduleEffects: HandleTrackSelected - Received action.", musicReceived, StringComparison.Ordinal);
        Assert.Contains("{PublicationCode}", musicReceived);

        Assert.Contains(
            "{CurrentId}",
            AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedDifferentMusicId);
        Assert.Contains(
            "{ActionId}",
            AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedSyncingAllowed);

        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedCouldNotResolveVocalLanguageDisplayName);

        var musicVmDispatch = AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedDispatchingUpdateScheduleFromViewModelMusic;
        Assert.Contains("{ScheduleId}", musicVmDispatch);
        Assert.Contains("{PublicationName}", musicVmDispatch);
        Assert.Contains("{TrackName}", musicVmDispatch);
    }

    [Fact]
    public void Logging_music_selection_container_and_android_media_session_templates()
    {
        Assert.StartsWith(
            "[MusicSelectionContainer]",
            AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ConstructorSubscribingPropertyChanged,
            StringComparison.Ordinal);
        Assert.Contains(
            "{ViewModelType}",
            AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ConstructorSubscribingPropertyChanged);

        Assert.Equal(
            "[MusicSelectionContainer] State unchanged, ignoring",
            AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.StateUnchangedIgnoring);

        Assert.Contains(
            "{ContainerName}",
            AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerFoundScrollViewScrollingToBottom);

        Assert.Contains(
            "{HasToken}",
            AppConstants.Logging.AndroidMediaSessionHelperDiagnosticsLog.MediaSessionCompatCreatedSuccessfullyInitialBufferingActiveHasToken);
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.AndroidMediaSessionHelperDiagnosticsLog.ApplyingLastPlayedMetadataToMediaSessionTitleArtistScheduleId);
        Assert.Contains(
            "{ArtworkUrl}",
            AppConstants.Logging.AndroidMediaSessionHelperDiagnosticsLog.LoadedArtworkBitmapFromArtworkUrl);
    }

    [Fact]
    public void Logging_schedule_display_name_music_and_bible_helper_templates()
    {
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.ErrorPopulatingMusicSectionNameForPublicationSection);
        Assert.Contains(
            "{SectionCode}",
            AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.MusicSectionNameNotFoundForPublicationSection);

        Assert.Contains(
            "{MusicSectionName}",
            AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.PopulatedMusicSectionNameForPublicationSection);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.NoLanguagePublicationKeepingLanguageNameForDisplay);
        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.NoLanguagePublicationKeepingLanguageNameForDisplay);

        Assert.Contains(
            "{CategoryId}",
            AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.PopulatedBiblePublicationCategoryIdAndCategoryName);
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.PopulatedBiblePublicationCategoryNameFromPublicationCode);

        var melody = AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.FailedToResolveMelodyTrackTitle;
        Assert.Contains("{PublicationCode}", melody);
        Assert.Contains("{TrackCode}", melody);
    }

    [Fact]
    public void Logging_default_schedule_service_foreground_notification_and_download_templates()
    {
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleUsingLastPlayedVerifiedInState);
        Assert.Contains(
            "{Index}",
            AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.GetNextScheduleInRotationRotatedToScheduleIndex);
        Assert.Contains(
            "{Title}",
            AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.SavedDefaultScheduleMetadataToPreferences);

        Assert.Contains(
            "{ArtworkPath}",
            AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.SavedDefaultScheduleArtworkToPathAndSize);
        Assert.Contains(
            "{HasArtwork}",
            AppConstants.Logging.DefaultScheduleServiceDiagnosticsLog.ReturningTrackMetadataForSchedule);

        Assert.Equal(
            "Failed to get app icon for alarm notification",
            AppConstants.Logging.AndroidForegroundNotificationDiagnosticsLog.FailedToGetAppIconForAlarmNotification);

        Assert.Contains("{Message}", AppConstants.Logging.DownloadDiagnosticsLog.SkippingRetryNetworkConnectivityFailure);
        Assert.Contains("{RetryCount}", AppConstants.Logging.DownloadDiagnosticsLog.RetryingDownloadAttemptAfterDelay);
        Assert.Contains("{AlternativeUrl}", AppConstants.Logging.DownloadDiagnosticsLog.FailedToDownloadPrimaryTryingAlternative);
        Assert.Contains("{Url}", AppConstants.Logging.DownloadDiagnosticsLog.HeadRequestFailedFallingBackToGet);
    }

    [Fact]
    public void Logging_media_cache_service_templates()
    {
        Assert.Contains("{ScheduleId}", AppConstants.Logging.MediaCacheDiagnosticsLog.SkippingCacheSetupInvalidScheduleId);

        var skipCached = AppConstants.Logging.MediaCacheDiagnosticsLog.SkippingDownloadCachedFileExists;
        Assert.Contains("{LookUpPath}", skipCached);
        Assert.Contains("{Url}", skipCached);

        var failedDl = AppConstants.Logging.MediaCacheDiagnosticsLog.FailedToDownloadTrackContinuingNext;
        Assert.Contains("{Url}", failedDl);
        Assert.Contains("{LookUpPath}", failedDl);
        Assert.Contains("{ScheduleId}", failedDl);

        var usingCached = AppConstants.Logging.MediaCacheDiagnosticsLog.UsingCachedFileForTrack;
        Assert.Contains("{CachedPath}", usingCached);

        Assert.Contains("{LookUpPath}", AppConstants.Logging.MediaCacheDiagnosticsLog.StreamingTrackFromCdnNotCached);
        Assert.Contains(
            "{LookUpPath}",
            AppConstants.Logging.MediaCacheDiagnosticsLog.CdnReturnedNotFoundRefetchingSectionPub);

        Assert.Equal(
            "An exception happened when downloading media files for caching.",
            AppConstants.Logging.MediaCacheDiagnosticsLog.ExceptionDownloadingMediaFilesForCaching);

        Assert.Equal(
            "Refetch did not yield a new URL; failing so UI can show error",
            AppConstants.Logging.MediaCacheDiagnosticsLog.RefetchDidNotYieldNewUrl);
    }

    [Fact]
    public void Logging_media_index_orphan_cleanup_and_media_service_templates()
    {
        var fileRetry = AppConstants.Logging.MediaIndexDiagnosticsLog.FileOperationFailedRetryingLocked;
        Assert.Contains("{RetryCount}", fileRetry);
        Assert.Contains("{DelayMs}", fileRetry);

        Assert.Contains(
            "{VersionFilePath}",
            AppConstants.Logging.MediaIndexDiagnosticsLog.FailedToReadVersionFromFile);
        Assert.Contains(
            "{Version}",
            AppConstants.Logging.MediaIndexDiagnosticsLog.SavedCurrentVersionToPreferences);

        Assert.Equal(
            "MediaIndexService: @lock disposed error.",
            AppConstants.Logging.MediaIndexDiagnosticsLog.LockDisposedError);

        var bibleOrphan = AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.BibleScheduleTrackNotInFetchedTablesDeletingSchedule;
        Assert.Contains("{ScheduleId}", bibleOrphan);
        Assert.Contains("{PubCode}", bibleOrphan);
        Assert.Contains("{SectionCode}", bibleOrphan);
        Assert.Contains("{TrackCode}", bibleOrphan);

        var cleanupCounts = AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.CleanedUpOrphanedSchedulesAndResetMusicCounts;
        Assert.Contains("{DeletedCount}", cleanupCounts);
        Assert.Contains("{ResetCount}", cleanupCounts);

        Assert.Contains(
            "{CategoryCode}",
            AppConstants.Logging.OrphanedScheduleCleanupDiagnosticsLog.AssignedCategoryCodeFromPublication);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.MediaServiceDiagnosticsLog.PublicationHasNullLanguageUsingSectionsWithoutLanguage);
        Assert.Contains(
            "{Count}",
            AppConstants.Logging.MediaServiceDiagnosticsLog.SuccessfullyLoadedSectionsForPublication);
        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsAvailableCodesFromPublicationLanguages);
        Assert.Contains(
            "{TotalCount}",
            AppConstants.Logging.MediaServiceDiagnosticsLog.GetBiblePublicationsReturningTotalCounts);
    }

    [Fact]
    public void Logging_media_service_vocal_releases_and_display_metadata_templates()
    {
        var vocalSummary = AppConstants.Logging.MediaServiceDiagnosticsLog.GetVocalMusicReleasesDownloadedCountSummary;
        Assert.Contains("{CountWithoutLang}", vocalSummary);
        Assert.Contains("{LanguageCode}", vocalSummary);

        var vocalTotals = AppConstants.Logging.MediaServiceDiagnosticsLog.GetVocalMusicReleasesReturningTotalCounts;
        Assert.Contains("{DownloadedCount}", vocalTotals);
        Assert.Contains("{PlaceholderCount}", vocalTotals);
        Assert.Contains("{TotalCount}", vocalTotals);

        Assert.Contains("{Uri}", AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractFileMetadataForArtworkArtistAlbum);
        Assert.Contains("{Context}", AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToExtractArtworkFromFileWithContext);

        var disc = AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToResolveMelodyDiscNameForPublicationDisc;
        Assert.Contains("{PublicationCode}", disc);
        Assert.Contains("{DiscCode}", disc);

        Assert.Equal(
            "Failed to get display metadata for track",
            AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToGetDisplayMetadataForTrack);
    }

    [Fact]
    public void Logging_application_reducer_schedule_sync_and_populate_song_publications_templates()
    {
        var vmLog = AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleFromViewModelLoggingStart;
        Assert.Contains("{ScheduleId}", vmLog);
        Assert.Contains("{PublicationName}", vmLog);
        Assert.Contains("{LanguageName}", vmLog);

        Assert.Contains(
            "{ActionType}",
            AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnDeleteScheduleReducerCalled);
        Assert.Contains(
            "{Error}",
            AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleFailure);

        var successEntry = AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnUpdateScheduleSuccessEntry;
        Assert.Contains("{MusicEnabled}", successEntry);
        Assert.Contains("{BiblePublicationTrackTitle}", successEntry);

        var bibleSel = AppConstants.Logging.ApplicationReducerDiagnosticsLog.OnBiblePublicationTrackSelectedUpdatedCurrentSchedule;
        Assert.Contains("{SectionCode}", bibleSel);
        Assert.Contains("{TrackCode}", bibleSel);
        Assert.Contains("{CategoryName}", bibleSel);

        var pubType = AppConstants.Logging.ApplicationReducerDiagnosticsLog.PreserveDisplayNamesPublicationTypeChanged;
        Assert.Contains("{ActionSectioned}", pubType);
        Assert.Contains("{ExistingSectioned}", pubType);

        var crudDel = AppConstants.Logging.ScheduleCrudReducerDiagnosticsLog.OnDeleteScheduleCalled;
        Assert.Contains("{ScheduleId}", crudDel);
        Assert.Contains("{IsNull}", crudDel);

        var idMismatch = AppConstants.Logging.ScheduleStateSyncHelperDiagnosticsLog.OnUpdateScheduleFromViewModelCurrentScheduleIdMismatch;
        Assert.Contains("{CurrentScheduleId}", idMismatch);
        Assert.Contains("{ActionScheduleId}", idMismatch);

        var preserveTime = AppConstants.Logging.ScheduleStateSyncHelperDiagnosticsLog.PreservingTimeFromExisting;
        Assert.Contains("{ActionHour}", preserveTime);
        Assert.Contains("{ExistingMinute}", preserveTime);

        var skipFetch = AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedAlreadyCatalogedSkippingFetch;
        Assert.Contains("{ExpectedCount}", skipFetch);
        Assert.Contains("{Category}", skipFetch);

        var noProgress = AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.NoProgressBetweenRetriesStopping;
        Assert.Contains("{CatalogedCount}", noProgress);
        Assert.Contains("{ExpectedCount}", noProgress);

        var removed = AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.RemovedUnfetchablePlaceholderPublications;
        Assert.Contains("{Count}", removed);
        Assert.Contains("{Codes}", removed);
    }

    [Fact]
    public void Logging_home_page_and_home_state_change_handler_templates()
    {
        Assert.Contains("{TotalMs}", AppConstants.Logging.HomePageDiagnosticsLog.BootstrapHomeFullyLoadedWithData);

        Assert.Contains("{IsBootstrapComplete}", AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleButtonClicked);
        Assert.Contains("{CanExecute}", AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleButtonClicked);

        Assert.Contains("{IsBootstrapComplete}", AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleCommandCannotExecute);

        Assert.Equal(
            "OnAddScheduleButtonClicked: Manually executing command",
            AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleManuallyExecutingCommand);

        var processing = AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.ProcessingSchedulesFromStateCurrentCollectionCount;
        Assert.Contains("{Count}", processing);
        Assert.Contains("{CurrentCount}", processing);

        var prep = AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PreparedAddRemoveTotalsHasSchedulesNow;
        Assert.Contains("{AddCount}", prep);
        Assert.Contains("{RemoveCount}", prep);
        Assert.Contains("{HasSchedulesNow}", prep);

        var adding = AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.AddingScheduleToCollectionScheduleIdAndName;
        Assert.Contains("{ScheduleId}", adding);
        Assert.Contains("{Name}", adding);

        Assert.Contains(
            "deferring list reorder until modal opens",
            AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedPlaybackModalVisibleDeferringListReorder);

        Assert.Equal(
            "OnStateChanged: State.Schedules is null, showing loading state",
            AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.StateSchedulesNullShowingLoading);
    }

    [Fact]
    public void Logging_busy_overlay_mini_playback_artwork_cdn_schedule_page_templates()
    {
        Assert.Contains("{OldValue}", AppConstants.Logging.BusyOverlayDiagnosticsLog.IsVisibleSetterFromTo);
        Assert.Contains("{NewValue}", AppConstants.Logging.BusyOverlayDiagnosticsLog.IsVisibleSetterFromTo);
        Assert.Contains("{TimeoutMs}", AppConstants.Logging.BusyOverlayDiagnosticsLog.HardTimeoutReachedAutoHiding);

        var binding = AppConstants.Logging.BusyOverlayDiagnosticsLog.OnIsVisibleChangedPropertyChangedBindingOpacity;
        Assert.Contains("{Opacity}", binding);
        Assert.Contains("{InputTransparent}", binding);

        Assert.Equal(
            "BusyOverlay: Starting spinner immediately",
            AppConstants.Logging.BusyOverlayDiagnosticsLog.StartingSpinnerImmediately);

        Assert.Contains("{ArtworkUrl}", AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorLoadingArtworkFromUrl);

        Assert.Contains("{FallbackUrl}", AppConstants.Logging.ArtworkManagerDiagnosticsLog.FallbackArtworkFailed);
        Assert.Contains("{FilePath}", AppConstants.Logging.ArtworkManagerDiagnosticsLog.FailedToLoadArtworkFromFile);

        Assert.Contains("{StatusCode}", AppConstants.Logging.CdnPlaybackUrlProbeDiagnosticsLog.HeadReturnedStatusTreatingAsIndeterminate);
        Assert.Equal(
            "CDN probe timed out for URL",
            AppConstants.Logging.CdnPlaybackUrlProbeDiagnosticsLog.ProbeTimedOutForUrl);

        var melodyFail = AppConstants.Logging.TrackCdnUrlRefresherDiagnosticsLog.MelodyDiscRefreshFailedPubDisc;
        Assert.Contains("{PublicationCode}", melodyFail);
        Assert.Contains("{Disc}", melodyFail);

        var apiFail = AppConstants.Logging.TrackCdnUrlRefresherDiagnosticsLog.ApiRefreshFailedPubLangSection;
        Assert.Contains("{LanguageCode}", apiFail);
        Assert.Contains("{SectionCode}", apiFail);

        Assert.Contains("{StartTime}", AppConstants.Logging.SchedulePageDiagnosticsLog.PerfConstructorStartedAt);
        Assert.Contains("{ElapsedMs}", AppConstants.Logging.SchedulePageDiagnosticsLog.PerfContentLoadCompletedInMs);

        Assert.Equal(
            "Schedule: WinUI UpdateLayout failed (best-effort)",
            AppConstants.Logging.SchedulePageDiagnosticsLog.WinUiUpdateLayoutFailedBestEffort);
    }

    [Fact]
    public void Logging_application_music_vocal_cascade_publication_chooser_and_playback_reducer_templates()
    {
        var musicTrack = AppConstants.Logging.ApplicationMusicReducerDiagnosticsLog.OnMusicTrackSelectedUpdatedCurrentSchedule;
        Assert.Contains("{LanguageCode}", musicTrack);
        Assert.Contains("{TrackName}", musicTrack);

        var musicSection = AppConstants.Logging.ApplicationMusicReducerDiagnosticsLog.OnMusicSectionSelectedUpdatedCurrentSchedule;
        Assert.Contains("{SectionCode}", musicSection);
        Assert.Contains("{SectionName}", musicSection);

        Assert.Contains(
            "{LanguageName}",
            AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.PopulateLanguagesMarkedLanguageSelected);
        Assert.Contains(
            "{Effective}",
            AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.RefreshLanguagesAsyncCurrentEffective);

        Assert.Equal(
            "MusicPublicationSelectionViewModel: CancelFetchCommand - User cancelled fetch",
            AppConstants.Logging.MusicPublicationSelectionViewModelDiagnosticsLog.CancelFetchCommandUserCancelledFetch);

        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.FirstPublicationByIdOrder);
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.DownloadingFirstVocalPublicationCascade);

        var autoAdv = AppConstants.Logging.PlaybackReducerDiagnosticsLog.AutoAdvancingFlagChanged;
        Assert.Contains("{PreviousValue}", autoAdv);
        Assert.Contains("{NewValue}", autoAdv);
        Assert.Contains("{ScheduleId}", autoAdv);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseSelectedPublicationNoLanguageNeeded);
        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionPublicationChooserDiagnosticsLog.ChooseFailedToCatalogPublicationTryingNext);
    }

    [Fact]
    public void Logging_bible_music_selection_state_handlers_and_view_schedule_command_templates()
    {
        var refreshTracks = AppConstants.Logging.BiblePublicationTrackSelectionViewModelDiagnosticsLog.RefreshFromStateLanguagePublicationSection;
        Assert.Contains("{LanguageCode}", refreshTracks);
        Assert.Contains("{PublicationCode}", refreshTracks);

        Assert.Contains(
            "{CurrentTrackCode}",
            AppConstants.Logging.BiblePublicationTrackSelectionViewModelDiagnosticsLog.InitializeLanguagePublicationSectionCurrentTrack);

        Assert.Contains(
            "{SectionCode}",
            AppConstants.Logging.TrackSelectionStateManagerDiagnosticsLog.HandleInitializedLanguagePublicationSection);
        Assert.Equal(
            "Error in TrackSelectionStateManager.HandleBiblePublicationChanged during track population",
            AppConstants.Logging.TrackSelectionStateManagerDiagnosticsLog.ErrorInHandleBiblePublicationChangedDuringTrackPopulation);

        Assert.Contains(
            "{Count}",
            AppConstants.Logging.BiblePublicationSelectionPropertyManagerDiagnosticsLog.MultipleLanguagesSelectedCount);

        Assert.Equal(
            "BiblePublicationSelectionViewModel: Fetch failed with network error",
            AppConstants.Logging.BiblePublicationSelectionViewModelDiagnosticsLog.FetchFailedNetworkError);

        var catNull = AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.HandleBiblePublicationChangedCategoryNullOrEmpty;
        Assert.Contains("{LanguageCode}", catNull);
        Assert.Contains(
            "{CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionStateHandlerDiagnosticsLog.RefreshFromStateGotCategoryFromPublicationLanguages);

        Assert.Contains(
            "{Context}",
            AppConstants.Logging.BiblePublicationSectionSelectionViewModelDiagnosticsLog.FaultedTaskContextTemplate);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.MusicPublicationSelectionCommandHandlerDiagnosticsLog.ErrorPublicationSelectionPublicationCode);
        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.MusicPublicationSelectionCommandHandlerDiagnosticsLog.NetworkErrorLanguageSelectionLanguageCode);

        Assert.Equal(
            "Error in HandleMusicChanged during publication population",
            AppConstants.Logging.MusicPublicationSelectionStateManagerDiagnosticsLog.ErrorInHandleMusicChangedDuringPublicationPopulation);

        Assert.Equal(
            "Error in MusicTrackStateManager.HandleMusicChanged during track population",
            AppConstants.Logging.MusicTrackStateManagerDiagnosticsLog.ErrorInHandleMusicChangedDuringTrackPopulation);

        var perfTap = AppConstants.Logging.ViewScheduleCommandDiagnosticsLog.PerfTapReceivedAtSchedule;
        Assert.Contains("{StartTime}", perfTap);
        Assert.Contains("{ScheduleId}", perfTap);
        Assert.Contains("{ElapsedMs}", AppConstants.Logging.ViewScheduleCommandDiagnosticsLog.PerfNavigationCompletedTotalMs);

        Assert.Equal(
            "IBatteryOptimizationService not available",
            AppConstants.Logging.ViewScheduleCommandDiagnosticsLog.BatteryOptimizationServiceNotAvailable);
    }

    [Fact]
    public void Logging_track_playback_handler_resume_and_seek_templates()
    {
        var cannotPlay = AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.CannotPlayTrackUriEmpty;
        Assert.Contains("{TrackIndex}", cannotPlay);
        Assert.Contains("{TrackUrl}", cannotPlay);

        Assert.Equal(
            "Playback was stopped during PrepareAsync/WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync",
            AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedDuringPrepareAborting);

        var resumeMem = AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekFromInMemoryFinishedDuration;
        Assert.Contains("{Duration}", resumeMem);
        Assert.Contains("{ScheduleId}", resumeMem);

        var resumeExceeds = AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekExceedsDurationStartingBeginning;
        Assert.Contains("{SeekPosition}", resumeExceeds);
        Assert.Contains("{Duration}", resumeExceeds);

        var seekBudget = AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekTotalBudgetExceeded;
        Assert.Contains("{Budget}", seekBudget);
        Assert.Contains("{Attempts}", seekBudget);

        var seekNoOp = AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptWasNoOpWillRetry;
        Assert.Contains("{Attempt}", seekNoOp);
        Assert.Contains("{Target}", seekNoOp);
        Assert.Contains("{Current}", seekNoOp);

        var seekInvalid = AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptFailedInvalidOperation;
        Assert.Contains("{Message}", seekInvalid);

        Assert.Contains("{MaxRetries}", AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAllAttemptsNoOpStartingBeginning);

        Assert.Contains("{Url}", AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.FailedToResolveTrackUriOnDemand);
        Assert.Contains("{LookUpPath}", AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.PreDownloadingNextTrackBackground);
        Assert.Contains("{Uri}", AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.PreDownloadCompleteUriResolved);

        Assert.Equal(
            "Pre-download of next track failed (non-critical)",
            AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.PreDownloadNextTrackFailedNonCritical);

        Assert.Contains(
            "{CategoryCode}",
            AppConstants.Logging.ScheduleDisplayMetadataDiagnosticsLog.FailedToResolveCategoryDisplayName);

        Assert.Equal(
            "[iOS NowPlaying] Failed to update metadata",
            AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToUpdateMetadata);
        Assert.Contains("{Url}", AppConstants.Logging.IosNowPlayingDiagnosticsLog.FailedToDownloadArtworkFromUrl);
    }

    [Fact]
    public void Logging_carplay_ios_app_delegate_audio_session_and_media_element_templates()
    {
        Assert.Equal(
            "[CarPlay] Connected to CarPlay interface controller",
            AppConstants.Logging.CarPlayDiagnosticsLog.ConnectedToInterfaceController);

        Assert.Contains("{Error}", AppConstants.Logging.CarPlayDiagnosticsLog.FailedToSetRootTemplateWithError);

        var tapped = AppConstants.Logging.CarPlayDiagnosticsLog.UserTappedSchedule;
        Assert.Contains("{Title}", tapped);
        Assert.Contains("{ScheduleId}", tapped);

        Assert.Contains("{Count}", AppConstants.Logging.CarPlayDiagnosticsLog.CreatedScheduleListTemplateWithCount);

        Assert.Equal(
            "[CarPlay] Cannot set root template - interface controller is null",
            AppConstants.Logging.CarPlayDiagnosticsLog.CannotSetRootTemplateInterfaceControllerNull);

        Assert.Contains("{ScheduleId}", AppConstants.Logging.IosAppDelegateDiagnosticsLog.NotificationTappedStartingPlayback);
        Assert.Contains("{Error}", AppConstants.Logging.IosAppDelegateDiagnosticsLog.FailedToResetBadgeCount);

        Assert.Equal(
            "ISchedulePlaybackService not available for notification playback",
            AppConstants.Logging.IosAppDelegateDiagnosticsLog.ISchedulePlaybackServiceNotAvailableNotificationPlayback);

        Assert.Contains("{Context}", AppConstants.Logging.IosAudioSessionDiagnosticsLog.AttemptingToConfigureAudioSessionForContext);
        var catFail = AppConstants.Logging.IosAudioSessionDiagnosticsLog.FailedToSetAvAudioSessionCategoryForContext;
        Assert.Contains("{Error}", catFail);

        Assert.Contains(
            "Bluetooth disconnected",
            AppConstants.Logging.IosAudioSessionDiagnosticsLog.AudioRouteChangedOldDeviceUnavailablePausingPlayback);

        Assert.Contains("{Uri}", AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.OriginalTrackUri);

        var normalized = AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.NormalizedPathWithOriginal;
        Assert.Contains("{Path}", normalized);
        Assert.Contains("{Original}", normalized);

        var setSource = AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.SetMediaElementSourceAndVolumeIos;
        Assert.Contains("{Source}", setSource);
        Assert.Contains("{State}", setSource);
    }

    [Fact]
    public void Logging_ios_media_element_play_retry_and_legacy_media_browser_service_templates()
    {
        var volBefore = AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.SetVolumeBeforePlayIos;
        Assert.Contains("{Volume}", volBefore);

        Assert.Contains(
            "{State}",
            AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.NotPlayingOrBufferingAfterPlayWaitingRetry);
        Assert.Contains(
            "{State}",
            AppConstants.Logging.IosMediaElementHelperDiagnosticsLog.AfterRetryMediaElementState);

        Assert.Contains("{ParentId}", AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnLoadChildrenCalledForParent);
        Assert.Contains("{Count}", AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.CreatedMediaItemsForAndroidAuto);

        Assert.Contains("{Action}", AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnBindCalledWithIntent);
        var intent = AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.IntentComponentPackageCategories;
        Assert.Contains("{Component}", intent);
        Assert.Contains("{Categories}", intent);

        Assert.Contains("{Token}", AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.SessionTokenSetInOnBind);

        Assert.Equal(
            "LegacyMediaBrowserService.OnCreate() called",
            AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.OnCreateCalled);

        Assert.Contains("{ParentId}", AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.FailedToSendEmptyResultForParent);
    }

    [Fact]
    public void Logging_legacy_aa_subscriptions_media_session_browse_and_android_auto_helpers_templates()
    {
        Assert.Contains(
            "{Count}",
            AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.OnApplicationStateChangedDetectedScheduleChangesNotifying);
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.DetectedScheduleRemoved);

        var notified = AppConstants.Logging.LegacyMediaBrowserStateSubscriptionDiagnosticsLog.NotifiedAndroidAutoOfScheduleChanges;
        Assert.Contains("{ChangeCount}", notified);
        Assert.Contains("{RemovedCount}", notified);

        Assert.Contains("{Token}", AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.SessionTokenSuccessfullySet);
        Assert.Contains(
            "Legacy Android Auto is connecting",
            AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.OnCreateCompletedLegacyAaConnectingSessionTokenOk);

        var addedItem = AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.AddedMediaItemForScheduleTitle;
        Assert.Contains("{ScheduleId}", addedItem);
        Assert.Contains("{Title}", addedItem);

        var iconSz = AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.SetSectionIconBitmapForScheduleSize;
        Assert.Contains("{Width}", iconSz);
        Assert.Contains("{Height}", iconSz);

        Assert.Contains(
            "{HeightPx}",
            AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.CreatedSectionIconBitmapSize);

        Assert.Contains("{MediaId}", AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.GettingMediaItemForId);
        Assert.Contains("{Query}", AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.SearchingForQuery);

        var getRoot = AppConstants.Logging.LegacyMediaBrowserClientValidatorDiagnosticsLog.OnGetRootCalledForClient;
        Assert.Contains("{ClientPackageName}", getRoot);
        Assert.Contains("{ClientUid}", getRoot);

        Assert.Contains("{Position}", AppConstants.Logging.LegacyMediaBrowserPlaybackControllerDiagnosticsLog.HandlingSeekToPosition);
        Assert.Contains("{MediaId}", AppConstants.Logging.LegacyMediaBrowserPlaybackControllerDiagnosticsLog.HandlingPlayFromMediaId);

        Assert.Contains("{Count}", AppConstants.Logging.AndroidAutoScheduleHelperDiagnosticsLog.LoadedSchedulesFromStateForAa);

        var changes = AppConstants.Logging.AndroidAutoScheduleChangeTrackerDiagnosticsLog.GetSpecificChangesChangesDetected;
        Assert.Contains("{OldCount}", changes);
        Assert.Contains("{NewCount}", changes);

        var sig = AppConstants.Logging.AndroidAutoScheduleChangeTrackerDiagnosticsLog.DetectedScheduleUpdatedSignatures;
        Assert.Contains("{ScheduleId}", sig);
        Assert.Contains("{OldSignature}", sig);
        Assert.Contains("{NewSignature}", sig);
    }

    [Fact]
    public void Logging_android_auto_rotation_modal_fetch_android_artwork_and_alarm_bootstrap_templates()
    {
        Assert.Contains(
            "{Minutes}",
            AppConstants.Logging.AndroidAutoDefaultScheduleRotationDiagnosticsLog.RotationStartedEveryMinutesWhenCarConnected);

        Assert.Contains(
            "cleaning up stale Android Auto state",
            AppConstants.Logging.AndroidAutoDefaultScheduleRotationDiagnosticsLog.CarDisconnectedBindFlagStaleCleanup);

        Assert.Equal(
            "Dispatching RotateDefaultScheduleAction for 5-minute rotation",
            AppConstants.Logging.AndroidAutoDefaultScheduleRotationDiagnosticsLog.DispatchingRotateDefaultScheduleActionFiveMinute);

        Assert.Equal(
            "[AndroidAuto] Error loading app icon fallback artwork",
            AppConstants.Logging.AndroidAutoPlayScreenDiagnosticsLog.ErrorLoadingAppIconFallbackArtwork);

        Assert.Equal(
            "Error in ModalScrollHelper.HandleModalAppearingAsync",
            AppConstants.Logging.ModalUiDiagnosticsLog.HandleModalAppearingAsyncError);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleItemStateServiceDiagnosticsLog.CouldNotSetIsBusyFalseForSchedule);

        Assert.Equal(
            "FetchProgressTracker.SetIsVisible: UI update failed (element may be disposed)",
            AppConstants.Logging.FetchProgressTrackerDiagnosticsLog.SetIsVisibleUiUpdateFailedElementMayBeDisposed);

        Assert.Contains("{ArtworkUrl}", AppConstants.Logging.AndroidMediaArtworkLog.ErrorLoadingBitmapFromArtworkUrl);
        Assert.Contains("{Duration}", AppConstants.Logging.AndroidMediaArtworkLog.DurationUpdatedInMetadataArtworkPreserved);

        Assert.Equal(
            "AndroidArtworkService not available - cannot load artwork",
            AppConstants.Logging.AndroidMediaArtworkLog.AndroidArtworkServiceNotAvailableCannotLoadArtwork);

        Assert.Equal(
            "Error processing scheduled tasks",
            AppConstants.Logging.AndroidAlarmBootstrapLog.SchedulerJobProcessingFailed);
    }

    [Fact]
    public void Logging_schedule_persistence_playback_event_and_media_element_handler_templates()
    {
        var saveStart = AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncStarting;
        Assert.Contains("{IsNewSchedule}", saveStart);
        Assert.Contains("{HasBiblePublication}", saveStart);

        Assert.Contains(
            "{Name}",
            AppConstants.Logging.SchedulePersistenceDiagnosticsLog.SaveScheduleAsyncErrorSaving);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.SchedulePersistenceDiagnosticsLog.CannotDeleteScheduleLastInDatabase);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleMediaCacheServiceDiagnosticsLog.ErrorSettingUpMediaCacheForSchedule);

        Assert.Equal(
            "Error initializing container view models",
            AppConstants.Logging.ScheduleContainerServiceDiagnosticsLog.ErrorInitializingContainerViewModels);

        Assert.Equal(
            "Error in HandleMediaFailedAsync",
            AppConstants.Logging.PlaybackDiagnosticsLog.ErrorInHandleMediaFailedAsync);

        var indefinite = AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.IndefiniteAdvancingToNextTrack;
        Assert.Contains("{ScheduleId}", indefinite);
        Assert.Contains("{NextIndex}", indefinite);
        Assert.Contains("{Count}", indefinite);

        var mediaFailed = AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.MediaFailedForTrackAtIndexUriUrl;
        Assert.Contains("{TrackIndex}", mediaFailed);
        Assert.Contains("{TrackUrl}", mediaFailed);

        Assert.Contains(
            "{FromTrackIndex}",
            AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.OnMediaEndedDispatchingSetAutoAdvancingForAutomaticNextTrack);

        Assert.Contains(
            "refreshed section/pub URLs",
            AppConstants.Logging.PlaybackEventHandlerDiagnosticsLog.PlaybackCdnUrlUnreachableRefreshedAutoReplayingSameTrackOnce);

        var meFail = AppConstants.Logging.MediaElementHandlerDiagnosticsLog.MediaElementFailedToPlayTrackUriSource;
        Assert.Contains("{TrackUri}", meFail);
        Assert.Contains("{Source}", meFail);

        Assert.Contains(
            "{Position}",
            AppConstants.Logging.MediaElementHandlerDiagnosticsLog.AudioPlayerOnSeekCompletedEventFiredResettingSeeking);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleListItemDiagnosticsLog.InitializeFromScheduleInvalidScheduleOrId);

        Assert.Contains(
            "{Count}",
            AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.UpdateScheduleViewModelsCalledWithCount);

        Assert.Contains(
            "{WeekDays}",
            AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.UpdatingExistingViewModelDaysOfWeek);
    }

    [Fact]
    public void Logging_schedule_vm_manager_tail_prepare_playback_and_playback_service_templates()
    {
        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.FailedToUpdateViewModelStateNotReady);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.ViewModelNotFoundCannotUpdate);

        var preparing = AppConstants.Logging.PreparePlaybackServiceDiagnosticsLog.PreparingFirstTrackForSchedule;
        Assert.Contains("{ScheduleId}", preparing);
        Assert.Contains("{LookUpPath}", preparing);

        Assert.Contains("{Url}", AppConstants.Logging.PreparePlaybackServiceDiagnosticsLog.FailedToResolveTrackUri);

        Assert.Equal(
            "Failed to get fallback alarm track",
            AppConstants.Logging.PlaybackFailureHandlerDiagnosticsLog.FailedToGetFallbackAlarmTrack);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.PlaybackServiceDiagnosticsLog.ErrorPreparingAndPlayingSchedule);

        var switchSched = AppConstants.Logging.PlaybackServiceDiagnosticsLog.StoppingExistingPlaybackBeforeStarting;
        Assert.Contains("{CurrentScheduleId}", switchSched);
        Assert.Contains("{ScheduleId}", switchSched);

        var forceReset = AppConstants.Logging.PlaybackServiceDiagnosticsLog.StateStillPlayingAfterStopForceResetting;
        Assert.Contains("{Status}", forceReset);

        Assert.Contains(
            "{TrackIndex}",
            AppConstants.Logging.PlaybackServiceDiagnosticsLog.CannotPlayTrackIndexOutOfRange);

        Assert.Contains(
            "{PlaylistCount}",
            AppConstants.Logging.PlaybackServiceDiagnosticsLog.CannotPlayTrackIndexOutOfRange);

        Assert.Contains(
            "timed out waiting for in-progress stop",
            AppConstants.Logging.PlaybackServiceDiagnosticsLog.PrepareAndPlayTimedOutWaitingForInFlightStop);

        Assert.Contains(
            "{PlayedTracksCount}",
            AppConstants.Logging.PlaybackStateManagerDiagnosticsLog.ResetClearedPlayedBibleTrackKeys);

        Assert.Equal(
            "Error disposing preparation cancellation token source",
            AppConstants.Logging.PlaybackStateManagerDiagnosticsLog.ErrorDisposingPreparationCancellationTokenSource);
    }

    [Fact]
    public void Logging_playback_stop_handler_playlist_builder_fallback_alarm_and_category_selection_templates()
    {
        Assert.Equal(
            "StopAsync called - stopping alarm completely",
            AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.StopAsyncCalledStoppingAlarmCompletely);

        Assert.Contains(
            "dispatching in finally",
            AppConstants.Logging.PlaybackStopHandlerDiagnosticsLog.PlaybackStoppedActionNotDispatchedDispatchingInFinally);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.SectionedSchedulePublicationSectionTrackCodes);

        var notInLookup = AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.TrackNotInLookupSectioned;
        Assert.Contains("{SectionCode}", notInLookup);
        Assert.Contains("{LanguageCode}", notInLookup);

        var resolved = AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.ResolvedNonSectionedTrack;
        Assert.Contains("{ResolvedTrackCode}", resolved);
        Assert.Contains("{LookUpPath}", resolved);

        var shouldSet = AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.ShouldSetFinishedDurationDetails;
        Assert.Contains("{ShouldSet}", shouldSet);
        Assert.Contains("{ScheduleFinishedDuration}", shouldSet);

        Assert.Contains(
            "{Duration}",
            AppConstants.Logging.PlaylistBiblePublicationTrackBuilderDiagnosticsLog.SetTrackFinishedDurationForSchedule);

        Assert.Equal(
            "Failed to get fallback alarm sound URI",
            AppConstants.Logging.FallbackAlarmSoundServiceDiagnosticsLog.FailedToGetFallbackAlarmSoundUri);

        Assert.Equal(
            "Error loading categories",
            AppConstants.Logging.CategorySelectionDiagnosticsLog.ErrorLoadingCategories);

        Assert.Contains(
            "{CategoryCode}",
            AppConstants.Logging.CategorySelectionDiagnosticsLog.ErrorDuringCategorySelectionCategoryCode);

        Assert.Contains(
            "{CategoryCode}",
            AppConstants.Logging.CategorySelectionDiagnosticsLog.LoadCategoriesAsyncMarkedCategorySelected);
    }

    [Fact]
    public void Logging_android_notification_media_session_and_navigation_service_templates()
    {
        Assert.Equal(
            "Failed to show toast message for exact alarm permission error",
            AppConstants.Logging.AndroidNotificationServiceDiagnosticsLog.FailedToShowToastExactAlarmPermissionError);

        Assert.Equal(
            "AlarmSetupService.OnCreate: failed to create MediaSession",
            AppConstants.Logging.AndroidMediaSessionCreationDiagnosticsLog.AlarmSetupServiceOnCreateFailed);

        Assert.Equal(
            "LegacyMediaBrowserService.OnCreate: failed to create MediaSession",
            AppConstants.Logging.AndroidMediaSessionCreationDiagnosticsLog.LegacyMediaBrowserServiceOnCreateFailed);

        Assert.Equal(
            "Error updating MediaSessionCompat playback state",
            AppConstants.Logging.AndroidMediaSessionCompatUpdateDiagnosticsLog.ErrorUpdatingPlaybackState);

        Assert.Contains(
            "{StartTime}",
            AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncStartAt);

        var pushPerf = AppConstants.Logging.NavigationServiceDiagnosticsLog.PerfNavigateToScheduleAsyncPushCompletedTotalSoFarMs;
        Assert.Contains("{ElapsedMs}", pushPerf);
        Assert.Contains("{TotalMs}", pushPerf);

        var popFinished = AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesFinishedDisposing;
        Assert.Contains("{ModalCount}", popFinished);
        Assert.Contains("{PageCount}", popFinished);

        var disposedPage = AppConstants.Logging.NavigationServiceDiagnosticsLog.PopAllModalsAndPagesDisposedPage;
        Assert.Contains("{PageType}", disposedPage);
        Assert.Contains("{PageTypeName}", disposedPage);
    }

    [Fact]
    public void Logging_navigation_stack_ios_cleanup_windows_toast_flyout_and_maui_platform_ui_templates()
    {
        Assert.Contains(
            "{Delay}",
            AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopModalAsyncFirstPopFailedRetry);

        Assert.Contains(
            "{Count}",
            AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopModalAsyncRetryPopFailed);

        Assert.Contains(
            "Home must never be removed",
            AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopAsyncRefusingPopHome);

        Assert.Contains(
            "{Type}",
            AppConstants.Logging.NavigationStackManagerDiagnosticsLog.DisconnectHandlersRecursivelyErrorDisconnectingHandlerForTypeNonFatal);

        Assert.Equal(
            "IosNativeViewCleanupHelper: Subviews walk hit disposed view (non-fatal)",
            AppConstants.Logging.IosNativeViewCleanupDiagnosticsLog.SubviewsWalkHitDisposedViewNonFatal);

        Assert.Equal(
            "Unable to create toast notifier. Scheduled notifications will not work. This is common in debug mode or when the app is not properly registered for notifications. Try running the app from an installed package instead of Visual Studio.",
            AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.UnableToCreateToastNotifierHints);

        Assert.Contains(
            "{HR:X8}",
            AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.COMExceptionCreatingNotifierWithoutParametersHResultTryingAumid);

        var notifierPackage = AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.FailedToCreateNotifierWithAnyAumidPackage;
        Assert.Contains("{PackageName}", notifierPackage);
        Assert.Contains("{FamilyName}", notifierPackage);

        Assert.Contains(
            "{AUMID}",
            AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.TryingToCreateNotifierWithAumid);

        var removeNotif = AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.FailedRemovingScheduledNotificationForSchedule;
        Assert.Contains("{NotificationId}", removeNotif);
        Assert.Contains("{ScheduleId}", removeNotif);

        Assert.Equal(
            "COM exception occurred while showing toast message",
            AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.COMExceptionShowingToastMessage);

        Assert.Contains(
            "{ArtworkUrl}",
            AppConstants.Logging.MauiPlatformUiDiagnosticsLog.WindowsToastXmlFailedToAddArtwork);
    }

    [Fact]
    public void Logging_maui_platform_ui_schedule_effects_bible_commands_and_list_item_vm_templates()
    {
        Assert.Equal(
            "[KeyboardHelper] Failed to hide keyboard on Android",
            AppConstants.Logging.MauiPlatformUiDiagnosticsLog.KeyboardHelperFailedToHideKeyboardAndroid);

        Assert.Equal(
            "MainApplication: Failed to create MediaSession",
            AppConstants.Logging.MauiPlatformUiDiagnosticsLog.AndroidMainApplicationFailedToCreateMediaSession);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.MauiPlatformUiDiagnosticsLog.WindowsNotificationFailedToastNotifierScheduleMsixHint);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleEffectsHelpersDiagnosticsLog.ErrorPopulatingModalCountsScheduleId);

        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.ScheduleEffectsHelpersDiagnosticsLog.ClearedMusicSectionForNonSectionedPublication);

        var populated = AppConstants.Logging.ScheduleEffectsHelpersDiagnosticsLog.PopulatedMusicSectionFromTrack;
        Assert.Contains("{MusicSectionCode}", populated);
        Assert.Contains("{TrackCode}", populated);

        Assert.Equal(
            "BibleSelectionContainerViewModel: SelectCategoryCommand - Opening category modal",
            AppConstants.Logging.BiblePublicationCommandInitializerDiagnosticsLog.SelectCategoryOpeningCategoryModal);

        Assert.Equal(
            "BibleSelectionContainerViewModel: SelectLanguageCommand - Opening language modal",
            AppConstants.Logging.BiblePublicationCommandInitializerDiagnosticsLog.SelectLanguageOpeningLanguageModal);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.PlayCommandFailedForSchedule);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.SpinnerTimedOutPlaybackActiveReRequestingModalLastResort);
    }

    [Fact]
    public void Logging_subtitle_manager_list_commands_remote_artwork_windows_notifications_and_app_lifecycle_templates()
    {
        var refreshDetail = AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshScheduleIdProvidedFoundPublicationSectionTrack;
        Assert.Contains("{ScheduleId}", refreshDetail);
        Assert.Contains("{HasProvidedItem}", refreshDetail);
        Assert.Contains("{TrackTitle}", refreshDetail);

        var built = AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.RefreshBuiltSubtitleForSchedule;
        Assert.Contains("{Subtitle}", built);

        Assert.Contains(
            "{HasSectionStructure}",
            AppConstants.Logging.ScheduleListItemSubtitleManagerDiagnosticsLog.WaitingForDisplayNamesToPopulate);

        Assert.Equal(
            "PreviousCommand: Schedule is null or has invalid ID",
            AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandScheduleNullOrInvalidId);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandMovingToPreviousTrack);

        Assert.Contains(
            "{ScheduleId}",
            AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandErrorMoving);

        Assert.Contains("{Url}", AppConstants.Logging.RemoteArtworkExtractorDiagnosticsLog.Id3ArtworkExtractionFailedForUrl);
        Assert.Contains("{TempPath}", AppConstants.Logging.RemoteArtworkExtractorDiagnosticsLog.Mp4FailedToDeleteTempFileFromTempPath);

        var schedFail = AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.FailedToScheduleNotificationAtFireDate;
        Assert.Contains("{ScheduleId}", schedFail);
        Assert.Contains("{FireDate}", schedFail);
        Assert.Contains("{ErrorMessage}", schedFail);

        var schedOk = AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.SuccessfullyScheduledCountForScheduleDays;
        Assert.Contains("{Count}", schedOk);
        Assert.Contains("{Days}", schedOk);

        var mediaToast = AppConstants.Logging.WindowsNotificationServiceDiagnosticsLog.MediaToastShownUpdatedClickActivatesApp;
        Assert.Contains("{Title}", mediaToast);
        Assert.Contains("{ArtworkUrl}", mediaToast);

        Assert.Contains(
            "{Status}",
            AppConstants.Logging.AppLifecycleDiagnosticsLog.ReconcilePlaybackStateFluxorActivePlayerInactiveDispatchStopped);

        Assert.Contains(
            "{Source}",
            AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorRecordingAppOpenFromSource);
    }

    [Fact]
    public void Notifications_toast_messages_and_permission_modal_copy_contract()
    {
        Assert.Equal("Press to start listening now.", AppConstants.Notifications.TapAlarmToListenBody);

        Assert.Equal(
            "Network may not be available, please try again",
            AppConstants.ToastMessages.NetworkMayNotBeAvailableTryAgain);
        Assert.Equal(
            "Cannot update the track when schedule is in progress",
            AppConstants.ToastMessages.CannotUpdateTrackScheduleInProgress);
        Assert.Equal("Select at least one day", AppConstants.ToastMessages.SelectAtLeastOneDay);
        Assert.Equal(
            "Schedule data is not ready, please try again",
            AppConstants.ToastMessages.ScheduleDataNotReadyTryAgain);
        Assert.Equal("Invalid schedule ID, please try again", AppConstants.ToastMessages.InvalidScheduleIdTryAgain);
        Assert.Equal("Cannot delete last schedule", AppConstants.ToastMessages.CannotDeleteLastSchedule);
        Assert.Equal("Schedule saved", AppConstants.ToastMessages.ScheduleSaved);
        Assert.Equal("Please check your internet connection", AppConstants.ToastMessages.PleaseCheckInternetConnection);
        Assert.Equal(
            "Notification permission is denied by Android",
            AppConstants.ToastMessages.NotificationPermissionDeniedByAndroid);
        Assert.Equal("Repeat enabled", AppConstants.ToastMessages.RepeatEnabled);
        Assert.Equal(
            "Cannot schedule reminder. Please enable 'Alarms & reminders' permission in system settings.",
            AppConstants.ToastMessages.CannotScheduleReminderExactAlarmPermission);
        Assert.Equal(
            "Notification permission is required for reminders on iOS. Please enable notifications in system settings.",
            AppConstants.ToastMessages.NotificationPermissionRequiredRemindersIos);
        Assert.Equal(
            "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.",
            AppConstants.ToastMessages.NotificationPermissionRequiredTapToPlayWinUi);

        Assert.Equal(
            "Notification permission is required for tap-to-play alarms. Please enable notifications to allow the app to show reminder notifications that you can tap to play alarms.",
            AppConstants.NotificationPermissionModalMessages.MainAndroidTapToPlayReminders);
        Assert.Equal(
            "Notification permission is required for alarms to work on iOS. Please enable notifications to allow the app to play alarms at scheduled times.",
            AppConstants.NotificationPermissionModalMessages.MainIosScheduledAlarms);
        Assert.Equal(
            "Notification permission is required for alarms. Please enable notifications.",
            AppConstants.NotificationPermissionModalMessages.MainOtherPlatforms);

        Assert.Equal(
            "REQUEST NOTIFICATION PERMISSION",
            AppConstants.NotificationPermissionModalMessages.RequestNotificationPermissionButtonLabel);
        Assert.Equal("OPEN APP SETTINGS", AppConstants.NotificationPermissionModalMessages.OpenAppSettingsButtonLabel);
        Assert.Equal("OPEN SETTINGS", AppConstants.NotificationPermissionModalMessages.OpenSettingsButtonLabel);

        Assert.Equal(
            "After clicking the button below, tap 'Allow' in the system permission dialog to enable notifications. If you've previously denied permission, use 'Open App Settings' to enable it in system settings.",
            AppConstants.NotificationPermissionModalMessages.InstructionsAndroidAfterAllowDialog);
        Assert.Equal(
            "After clicking the button below, tap 'Allow' in the system permission dialog to enable notifications. If you've previously denied permission, use 'Open Settings' to enable it in system settings.",
            AppConstants.NotificationPermissionModalMessages.InstructionsIosAfterAllowDialog);
        Assert.Equal(
            "Please enable notifications in system settings.",
            AppConstants.NotificationPermissionModalMessages.InstructionsOtherPlatformsEnableInSettings);
    }

    [Fact]
    public void Sample_schedule_diagnostics_car_play_list_and_media_core_contract()
    {
        Assert.Equal(
            "No Bible publications found",
            AppConstants.SampleScheduleDiagnostics.MessageContainsNoBiblePublications);
        Assert.Equal(
            "No Bible publications found in database",
            AppConstants.SampleScheduleDiagnostics.NoBiblePublicationsInDatabaseMessage);
        Assert.Equal(
            "No sectioned Bible publication",
            AppConstants.SampleScheduleDiagnostics.MessageContainsNoSectionedPublication);
        Assert.Equal(
            "No sectioned Bible publication found in database for sample schedule",
            AppConstants.SampleScheduleDiagnostics.NoSectionedPublicationForSampleScheduleMessage);

        Assert.Equal("Schedules", AppConstants.CarPlayScheduleList.SectionTitleSchedules);
        Assert.Equal(
            "Loading schedules…",
            AppConstants.CarPlayScheduleList.LoadingPrimaryText);
        Assert.Equal(
            "Schedules will appear once the app is ready",
            AppConstants.CarPlayScheduleList.LoadingSecondaryText);

        Assert.Equal("E", AppConstants.Media.DefaultLanguageCode);
        Assert.Equal("English", AppConstants.Media.DefaultLanguageDisplayNameEnglish);
        Assert.Equal("LAH", AppConstants.Media.LanguageCodePatchFrom);
        Assert.Equal("LAHU", AppConstants.Media.LanguageCodePatchTo);

        Assert.Equal(".mp3", AppConstants.Media.MediaFileExtension);
        Assert.Equal(".mp4", AppConstants.Media.MediaVideoFileExtension);
        Assert.Equal(".m4a", AppConstants.Media.MediaM4aFileExtension);
        Assert.Equal(".aac", AppConstants.Media.MediaAacFileExtension);

        Assert.Equal(
            "BibleAlarm/1.0 (compatible; iOS; MAUI)",
            AppConstants.Media.MediaHttpUserAgent);
        Assert.Equal("*/*", AppConstants.Media.HttpAcceptAny);

        Assert.Equal("403", AppConstants.Media.DownloadPermanentFailureHttpFragments.StatusCode403);
        Assert.Equal("Not Found", AppConstants.Media.DownloadPermanentFailureHttpFragments.NotFound);

        Assert.Equal(
            "Mozilla/5.0 (compatible; curl/8.0.1)",
            AppConstants.Media.CatalogerHttpUserAgent);
        Assert.Equal(
            "application/json, text/plain, */*",
            AppConstants.Media.CatalogerHttpAcceptHeader);

        Assert.Equal("MP3", AppConstants.Media.MediaStreamFormatMp3);
        Assert.Equal("MP4", AppConstants.Media.MediaStreamFormatMp4);
        Assert.Equal("M4A", AppConstants.Media.MediaStreamFormatM4a);
        Assert.Equal("AAC", AppConstants.Media.MediaStreamFormatAac);
        Assert.Equal("240p", AppConstants.Media.VideoQualityLabel240p);

        Assert.Equal("files", AppConstants.Media.PubMediaJson.Files);
        Assert.Equal("progressiveDownloadURL", AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl);
        Assert.Equal("media", AppConstants.Media.PubMediaJson.CategoryMedia);

        Assert.Equal("languages", AppConstants.Media.LanguageIndexJson.Languages);
        Assert.Equal("langcode", AppConstants.Media.LanguageIndexJson.LangCode);

        Assert.Equal("output=json", AppConstants.Media.GetPubQueryOutputJson);
        Assert.Equal("alllangs=0", AppConstants.Media.GetPubQueryAllLangsOff);
        Assert.Equal("pub", AppConstants.Media.GetPubQueryParamName.Pub);
        Assert.Equal("category", AppConstants.Media.MediatorQueryParamName.Category);

        Assert.Equal("docid:", AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix);
    }

    [Fact]
    public void Media_mediator_id_prefix_publication_schedule_ui_playback_modal_and_good_news_contract()
    {
        Assert.Equal("docid-", AppConstants.Media.MediatorIdentifiers.DocIdNaturalKeyPrefix);
        Assert.Equal("pub-", AppConstants.Media.MediatorIdentifiers.PubNaturalKeyPrefix);

        Assert.Equal("Track", AppConstants.Media.PublicationUiTrackSingular);
        Assert.Equal("Tracks", AppConstants.Media.PublicationUiTrackPlural);
        Assert.Equal("Part", AppConstants.Media.PublicationUiPartSingular);
        Assert.Equal("Episode", AppConstants.Media.PublicationUiEpisodeSingular);
        Assert.Equal("Episodes", AppConstants.Media.PublicationUiEpisodePlural);
        Assert.Equal("Chapter", AppConstants.Media.PublicationUiChapterSingular);
        Assert.Equal("Chapters", AppConstants.Media.PublicationUiChapterPlural);

        Assert.Equal("New schedule", AppConstants.Media.ScheduleUiSampleNameNew);
        Assert.Equal("Schedule Name", AppConstants.Media.ScheduleUiSampleNamePlaceholder);
        Assert.Equal("Unnamed schedule", AppConstants.Media.ScheduleUiUnnamedPlaceholder);
        Assert.Equal("Enabled", AppConstants.Media.ScheduleUiStatusEnabled);
        Assert.Equal("Disabled", AppConstants.Media.ScheduleUiStatusDisabled);
        Assert.Equal("MusicLanguageDisplayText", AppConstants.Media.ScheduleMusicLanguageDisplayTextPropertyName);

        Assert.Equal(
            "Playback failed. Tap Retry.",
            AppConstants.Media.PlaybackModalMessages.PlaybackFailedTapRetry);
        Assert.Equal(
            "Could not load the next part. Check your connection, then tap Retry.",
            AppConstants.Media.PlaybackModalMessages.CouldNotLoadNextPartCheckConnectionTapRetry);
        Assert.Equal(
            "Playback stopped (connection lost or interrupted). Tap Retry.",
            AppConstants.Media.PlaybackModalMessages.PlaybackStoppedConnectionLostTapRetry);
        Assert.Equal(
            "Could not start playback (network or server busy). Tap Retry.",
            AppConstants.Media.PlaybackModalMessages.CouldNotStartPlaybackNetworkBusyTapRetry);
        Assert.Equal(
            "Could not update playback links. Tap Retry.",
            AppConstants.Media.PlaybackModalMessages.CouldNotUpdatePlaybackLinksTapRetry);
        Assert.Equal(
            "Still could not play after updating links. Tap Retry.",
            AppConstants.Media.PlaybackModalMessages.StillCouldNotPlayAfterUpdatingLinksTapRetry);
        Assert.Equal(
            "Could not start playback. Check your connection, then tap Retry.",
            AppConstants.Media.PlaybackModalMessages.CouldNotStartPlaybackCheckConnectionTapRetry);

        Assert.Equal("Ready to play", AppConstants.Media.NowPlayingPlaceholder.ArtistReadyToPlay);
        Assert.Equal("Tap to play", AppConstants.Media.NowPlayingPlaceholder.ArtistTapToPlay);
        Assert.Equal("...", AppConstants.Media.NowPlayingPlaceholder.AlbumEllipsis);

        Assert.Equal("Volume ", AppConstants.Media.PublicationUiMelodyVolumePrefix);

        Assert.Equal(
            "The Good News According to Jesus",
            AppConstants.Media.PublicationDisplayNameGoodNewsAccordingToJesus);
        Assert.Equal(
            "Good news according to Jesus",
            AppConstants.Media.ApiMisleadingGoodNewsPublicationPhraseAlternate);
        Assert.Equal(2, AppConstants.Media.ApiMisleadingGoodNewsVideoPublicationNamePhrases.Length);
        Assert.Contains(
            AppConstants.Media.PublicationDisplayNameGoodNewsAccordingToJesus,
            AppConstants.Media.ApiMisleadingGoodNewsVideoPublicationNamePhrases);
    }

    [Fact]
    public void Media_publication_catalog_categories_mediator_keys_and_broadcast_publication_contract()
    {
        Assert.Equal("Music", AppConstants.Media.BiblePublicationCategoryMusic);
        Assert.Equal("osg", AppConstants.Media.MusicPublicationCodeOsg);
        Assert.Equal("iam", AppConstants.Media.MelodyMusicPublicationCodeIam);
        Assert.Equal("Kingdom Melodies", AppConstants.Media.PublicationDisplayNameKingdomMelodies);

        Assert.Equal("Bible", AppConstants.Media.BiblePublicationCategoryBible);
        Assert.Equal("nwt", AppConstants.Media.BiblePublicationCodeNwt);
        Assert.Equal("bi12", AppConstants.Media.BiblePublicationCodeBi12);
        Assert.Equal("1", AppConstants.Media.BiblePublicationGenesisBookNumber);

        Assert.Equal("Books", AppConstants.Media.CatalogFlatAudioLabelBooks);
        Assert.Equal("Yearbooks", AppConstants.Media.CatalogFlatAudioLabelYearbooks);
        Assert.Equal("Brochures and Booklets", AppConstants.Media.CatalogFlatAudioLabelBrochuresAndBooklets);

        Assert.Equal("Dramas", AppConstants.Media.BiblePublicationCategoryDramas);
        Assert.Equal("DramaticBibleReadings", AppConstants.Media.BiblePublicationCodeDramaticBibleReadings);
        Assert.Equal("DramasGoodNews", AppConstants.Media.BiblePublicationCodeDramasGoodNews);
        Assert.Equal("VODMoviesBibleTimes", AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes);

        Assert.Equal("WatchtowerMagazine", AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);
        Assert.Equal("Series", AppConstants.Media.BiblePublicationCategorySeries);
        Assert.Equal("ArticleSeries", AppConstants.Media.BiblePublicationCategoryArticleSeries);

        Assert.Equal("2014Convention", AppConstants.Media.MediatorCategoryKey2014Convention);
        Assert.Equal("2025Convention", AppConstants.Media.MediatorCategoryKey2025Convention);

        Assert.Equal("ChildrenMovies", AppConstants.Media.MediatorCategoryKeyChildrenMovies);
        Assert.Equal("TeenSocialLife", AppConstants.Media.MediatorCategoryKeyTeenSocialLife);
        Assert.Equal("BibleBooks", AppConstants.Media.MediatorCategoryKeyBibleBooks);

        Assert.Equal("StudioMonthlyPrograms", AppConstants.Media.MediatorPublicationCodeStudioMonthlyPrograms);
        Assert.Equal("VODBibleReadingStudy", AppConstants.Media.MediatorPublicationCodeVODBibleReadingStudy);
        Assert.Equal("VODPgmEvtMorningWorship", AppConstants.Media.MediatorPublicationCodeVODPgmEvtMorningWorship);
    }

    [Fact]
    public void Media_mediator_publication_codes_studio_vod_series_and_specialized_vod_buckets_contract()
    {
        Assert.Equal("StudioTalks", AppConstants.Media.MediatorPublicationCodeStudioTalks);
        Assert.Equal("StudioNewsReports", AppConstants.Media.MediatorPublicationCodeStudioNewsReports);

        Assert.Equal("VODPgmEvtSpecial", AppConstants.Media.MediatorPublicationCodeVODPgmEvtSpecial);
        Assert.Equal("VODPgmEvtGilead", AppConstants.Media.MediatorPublicationCodeVODPgmEvtGilead);
        Assert.Equal("VODPgmEvtAnnMtg", AppConstants.Media.MediatorPublicationCodeVODPgmEvtAnnMtg);

        Assert.Equal("VODBibleTeachings", AppConstants.Media.MediatorPublicationCodeVODBibleTeachings);
        Assert.Equal("VODBibleAccounts", AppConstants.Media.MediatorPublicationCodeVODBibleAccounts);
        Assert.Equal("VODBibleMedia", AppConstants.Media.MediatorPublicationCodeVODBibleMedia);
        Assert.Equal("VODBibleTranslations", AppConstants.Media.MediatorPublicationCodeVODBibleTranslations);
        Assert.Equal("VODBiblePrinciples", AppConstants.Media.MediatorPublicationCodeVODBiblePrinciples);
        Assert.Equal("VODBibleCreation", AppConstants.Media.MediatorPublicationCodeVODBibleCreation);

        Assert.Equal("FamilyChallenges", AppConstants.Media.MediatorPublicationCodeFamilyChallenges);
        Assert.Equal("FamilyDatingMarriage", AppConstants.Media.MediatorPublicationCodeFamilyDatingMarriage);

        Assert.Equal("BJF", AppConstants.Media.MediatorPublicationCodeBJF);
        Assert.Equal("SeriesBJFSongs", AppConstants.Media.MediatorPublicationCodeSeriesBJFSongs);

        Assert.Equal("VODActivitiesTranslation", AppConstants.Media.MediatorPublicationCodeVODActivitiesTranslation);
        Assert.Equal("VODActivitiesAVProduction", AppConstants.Media.MediatorPublicationCodeVODActivitiesAVProduction);
        Assert.Equal("VODActivitiesPrintingShipping", AppConstants.Media.MediatorPublicationCodeVODActivitiesPrintingShipping);
        Assert.Equal("VODActivitiesConstruction", AppConstants.Media.MediatorPublicationCodeVODActivitiesConstruction);
        Assert.Equal("VODActivitiesReliefWork", AppConstants.Media.MediatorPublicationCodeVODActivitiesReliefWork);
        Assert.Equal("VODActivitiesTheoSchools", AppConstants.Media.MediatorPublicationCodeVODActivitiesTheoSchools);
        Assert.Equal("VODActivitiesSpecialEvents", AppConstants.Media.MediatorPublicationCodeVODActivitiesSpecialEvents);

        Assert.Equal("VODMinistryTools", AppConstants.Media.MediatorPublicationCodeVODMinistryTools);
        Assert.Equal("VODMinistryImproveSkills", AppConstants.Media.MediatorPublicationCodeVODMinistryImproveSkills);
        Assert.Equal("VODMinistryMethods", AppConstants.Media.MediatorPublicationCodeVODMinistryMethods);
        Assert.Equal("MeetingsConventions", AppConstants.Media.MediatorPublicationCodeMeetingsConventions);
        Assert.Equal("VODSampleConversations", AppConstants.Media.MediatorPublicationCodeVODSampleConversations);

        Assert.Equal("Reports", AppConstants.Media.MediatorPublicationCodeReports);
        Assert.Equal("VODOrgBethel", AppConstants.Media.MediatorPublicationCodeVODOrgBethel);
        Assert.Equal("AccomplishMinistry", AppConstants.Media.MediatorPublicationCodeAccomplishMinistry);
        Assert.Equal("VODOrgHistory", AppConstants.Media.MediatorPublicationCodeVODOrgHistory);
        Assert.Equal("VODOrgLegal", AppConstants.Media.MediatorPublicationCodeVODOrgLegal);
        Assert.Equal("VODOrgBloodlessMedicine", AppConstants.Media.MediatorPublicationCodeVODOrgBloodlessMedicine);

        Assert.Equal("VODIntExpTransformations", AppConstants.Media.MediatorPublicationCodeVODIntExpTransformations);
        Assert.Equal("VODIntExpBlessings", AppConstants.Media.MediatorPublicationCodeVODIntExpBlessings);
        Assert.Equal("VODIntExpEndurance", AppConstants.Media.MediatorPublicationCodeVODIntExpEndurance);
        Assert.Equal("VODIntExpYouth", AppConstants.Media.MediatorPublicationCodeVODIntExpYouth);
        Assert.Equal("OriginsLife", AppConstants.Media.MediatorPublicationCodeOriginsLife);
        Assert.Equal("VODIntExpArchives", AppConstants.Media.MediatorPublicationCodeVODIntExpArchives);

        Assert.Equal("VODConvMusic", AppConstants.Media.MediatorPublicationCodeVODConvMusic);
        Assert.Equal("MakingMusic", AppConstants.Media.MediatorPublicationCodeMakingMusic);
        Assert.Equal("VODSingToJah", AppConstants.Media.MediatorPublicationCodeVODSingToJah);

        Assert.Equal("SeriesBibleTeachings", AppConstants.Media.MediatorPublicationCodeSeriesBibleTeachings);
        Assert.Equal("SeriesHappyMarriage", AppConstants.Media.MediatorPublicationCodeSeriesHappyMarriage);
        Assert.Equal("SeriesImitateFaith", AppConstants.Media.MediatorPublicationCodeSeriesImitateFaith);
        Assert.Equal("SeriesIronSharpens", AppConstants.Media.MediatorPublicationCodeSeriesIronSharpens);
        Assert.Equal("SeriesJehovahsFriends", AppConstants.Media.MediatorPublicationCodeSeriesJehovahsFriends);
        Assert.Equal("SeriesLearnFromThem", AppConstants.Media.MediatorPublicationCodeSeriesLearnFromThem);
        Assert.Equal("SeriesWTLessons", AppConstants.Media.MediatorPublicationCodeSeriesWTLessons);
        Assert.Equal("VODLovePeople", AppConstants.Media.MediatorPublicationCodeVODLovePeople);
        Assert.Equal("SeriesMyTeenLife", AppConstants.Media.MediatorPublicationCodeSeriesMyTeenLife);
        Assert.Equal("SeriesNeetaJade", AppConstants.Media.MediatorPublicationCodeSeriesNeetaJade);
        Assert.Equal("SeriesOrgAccomplishments", AppConstants.Media.MediatorPublicationCodeSeriesOrgAccomplishments);
        Assert.Equal("SeriesOurHistory", AppConstants.Media.MediatorPublicationCodeSeriesOurHistory);
        Assert.Equal("VODPureWorshipIntro", AppConstants.Media.MediatorPublicationCodeVODPureWorshipIntro);
        Assert.Equal("SeriesBibleChangesLives", AppConstants.Media.MediatorPublicationCodeSeriesBibleChangesLives);
        Assert.Equal("SeriesGoodNews", AppConstants.Media.MediatorPublicationCodeSeriesGoodNews);
        Assert.Equal("SeriesTruthTransforms", AppConstants.Media.MediatorPublicationCodeSeriesTruthTransforms);
        Assert.Equal("SeriesOriginsLife", AppConstants.Media.MediatorPublicationCodeSeriesOriginsLife);
        Assert.Equal("SeriesWCGVideos", AppConstants.Media.MediatorPublicationCodeSeriesWCGVideos);
        Assert.Equal("SeriesWasItDesigned", AppConstants.Media.MediatorPublicationCodeSeriesWasItDesigned);
        Assert.Equal("SeriesWhereAreTheyNow", AppConstants.Media.MediatorPublicationCodeSeriesWhereAreTheyNow);
        Assert.Equal("SeriesWhiteboard", AppConstants.Media.MediatorPublicationCodeSeriesWhiteboard);
    }

    [Fact]
    public void Media_normalized_publication_codes_display_fallbacks_vocal_flat_getpub_and_text_direction_contract()
    {
        Assert.Equal("dramasgoodnews", AppConstants.Media.NormalizedPublicationCodeDramasGoodNews);
        Assert.Equal("vodmoviesbibletimes", AppConstants.Media.NormalizedPublicationCodeVODMoviesBibleTimes);
        Assert.Equal("vodmoviesmodernday", AppConstants.Media.NormalizedPublicationCodeVODMoviesModernDay);
        Assert.Equal("vodmoviesanimated", AppConstants.Media.NormalizedPublicationCodeVODMoviesAnimated);
        Assert.Equal("vodmoviesextras", AppConstants.Media.NormalizedPublicationCodeVODMoviesExtras);
        Assert.Equal("seriesdigfortreasures", AppConstants.Media.NormalizedPublicationCodeSeriesDigForTreasures);
        Assert.Equal("seriesbjflessons", AppConstants.Media.NormalizedPublicationCodeSeriesBJFLessons);
        Assert.Equal("vodlffvideosad", AppConstants.Media.NormalizedPublicationCodeVodLffVideosAd);
        Assert.Equal("bodlffvideosad", AppConstants.Media.NormalizedPublicationCodeBodLffVideosAd);

        Assert.Equal(
            "Enjoy Life Forever!—Videos",
            AppConstants.Media.PublicationDisplayNameEnjoyLifeForeverVideos);
        Assert.Equal(
            "Dig for Treasures in God's Word",
            AppConstants.Media.PublicationDisplayNameDigForTreasuresInGodsWord);
        Assert.Equal(
            "Bible Stories for Little Ones",
            AppConstants.Media.PublicationDisplayNameBibleStoriesForLittleOnes);

        Assert.Equal(
            new[]
            {
                AppConstants.Media.MusicPublicationCodeOsg,
                AppConstants.Media.MusicPublicationCodeSjjc,
                AppConstants.Media.MusicPublicationCodeSjji,
                AppConstants.Media.MusicPublicationCodeSnv,
                AppConstants.Media.MusicPublicationCodePksjj,
            },
            AppConstants.Media.VocalMusicCatalogPublicationCodes);

        Assert.Equal(
            new[]
            {
                "wcg", "lff", "rr", "lvs", "lfb", "bhs", "jy", "kr", "ia", "mb", "jr", "bt", "lv", "cf",
                "jd", "bh", "my", "lr", "cl", "fy", "gt",
            },
            AppConstants.Media.FlatMp3BooksPublicationCodes);

        Assert.Equal(
            new[] { "yb17", "yb16", "yb15", "yb14", "yb13", "yb12", "yb11", "yb10" },
            AppConstants.Media.FlatMp3YearbooksPublicationCodes);

        Assert.Equal(
            new[]
            {
                "lmd", "wfg", "lffi", "th", "rj", "ypq", "hf", "jl", "yc", "hl", "fg", "ll", "lc", "lf",
                "la", "we",
            },
            AppConstants.Media.FlatMp3BrochuresPublicationCodes);

        Assert.Equal(new[] { "mrt", "hdu", "lfs" }, AppConstants.Media.FlatMp3ArticleSeriesPublicationCodes);

        Assert.Equal(
            new[]
            {
                "mwbv", "jwb", "jwbrd", "jwbiv", "jwbcov", "jwbam", "jwbur", "jwbls", "jwbgg",
            },
            AppConstants.Media.GetPubIssueParameterPublicationCodes);

        Assert.Equal(new[] { "ivdd", "ivno" }, AppConstants.Media.GetPubSingleTrackNoParamPublicationCodes);
        Assert.Equal(new[] { "bhat" }, AppConstants.Media.GetPubSingleTrackZeroPublicationCodes);

        Assert.Equal("ltr", AppConstants.Media.TextDirectionLeftToRight);
        Assert.Equal("rtl", AppConstants.Media.TextDirectionRightToLeft);
    }

    [Fact]
    public void Media_additional_categories_publication_codes_series_thv_mediator_conventions_aggregators_contract()
    {
        Assert.Equal("thv", AppConstants.Media.SeriesPublicationCodeThv);

        Assert.Equal("Article Series", AppConstants.Media.CatalogFlatAudioLabelArticleSeries);

        Assert.Equal("AwakeMagazine", AppConstants.Media.BiblePublicationCategoryAwakeMagazine);
        Assert.Equal("VODMoviesModernDay", AppConstants.Media.BiblePublicationCodeVODMoviesModernDay);
        Assert.Equal("VODMoviesAnimated", AppConstants.Media.BiblePublicationCodeVODMoviesAnimated);
        Assert.Equal("VODMoviesExtras", AppConstants.Media.BiblePublicationCodeVODMoviesExtras);
        Assert.Equal("SeriesDigForTreasures", AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures);
        Assert.Equal("SeriesBJFLessons", AppConstants.Media.BiblePublicationCodeSeriesBJFLessons);
        Assert.Equal("SeriesWhatPeersSay", AppConstants.Media.BiblePublicationCodeSeriesWhatPeersSay);

        Assert.Equal("FaithAndBible", AppConstants.Media.BiblePublicationCategoryFaithAndBible);
        Assert.Equal("Books", AppConstants.Media.BiblePublicationCategoryBooks);
        Assert.Equal("Yearbooks", AppConstants.Media.BiblePublicationCategoryYearbooks);
        Assert.Equal("Broadcasting", AppConstants.Media.BiblePublicationCategoryBroadcasting);
        Assert.Equal("BrochuresAndBooklets", AppConstants.Media.BiblePublicationCategoryBrochuresAndBooklets);
        Assert.Equal("Children", AppConstants.Media.BiblePublicationCategoryChildren);
        Assert.Equal("Family", AppConstants.Media.BiblePublicationCategoryFamily);
        Assert.Equal(
            "InterviewsAndExperiences",
            AppConstants.Media.BiblePublicationCategoryInterviewsAndExperiences);
        Assert.Equal("MeetingsAndMinistry", AppConstants.Media.BiblePublicationCategoryMeetingsAndMinistry);
        Assert.Equal("ProgramsAndEvents", AppConstants.Media.BiblePublicationCategoryProgramsAndEvents);
        Assert.Equal("Teenagers", AppConstants.Media.BiblePublicationCategoryTeenagers);
        Assert.Equal("Activities", AppConstants.Media.BiblePublicationCategoryActivities);
        Assert.Equal("Organization", AppConstants.Media.BiblePublicationCategoryOrganization);

        Assert.Equal("2015Convention", AppConstants.Media.MediatorCategoryKey2015Convention);
        Assert.Equal("2016Convention", AppConstants.Media.MediatorCategoryKey2016Convention);
        Assert.Equal("2017Convention", AppConstants.Media.MediatorCategoryKey2017Convention);
        Assert.Equal("2018Convention", AppConstants.Media.MediatorCategoryKey2018Convention);
        Assert.Equal("2019Convention", AppConstants.Media.MediatorCategoryKey2019Convention);
        Assert.Equal("2020Convention", AppConstants.Media.MediatorCategoryKey2020Convention);
        Assert.Equal("2021Convention", AppConstants.Media.MediatorCategoryKey2021Convention);
        Assert.Equal("2022Convention", AppConstants.Media.MediatorCategoryKey2022Convention);
        Assert.Equal("2023Convention", AppConstants.Media.MediatorCategoryKey2023Convention);
        Assert.Equal("2024Convention", AppConstants.Media.MediatorCategoryKey2024Convention);

        Assert.Equal("ChildrenSongs", AppConstants.Media.MediatorCategoryKeyChildrenSongs);
        Assert.Equal("FamilyMovies", AppConstants.Media.MediatorCategoryKeyFamilyMovies);
        Assert.Equal("FamilyWorship", AppConstants.Media.MediatorCategoryKeyFamilyWorship);
        Assert.Equal("TeenMovies", AppConstants.Media.MediatorCategoryKeyTeenMovies);
        Assert.Equal("TeenGoals", AppConstants.Media.MediatorCategoryKeyTeenGoals);
        Assert.Equal("TeenSpiritualGrowth", AppConstants.Media.MediatorCategoryKeyTeenSpiritualGrowth);
        Assert.Equal("TeenWhatPeersSay", AppConstants.Media.MediatorCategoryKeyTeenWhatPeersSay);
        Assert.Equal("SeriesBibleBooks", AppConstants.Media.MediatorCategoryKeySeriesBibleBooks);
    }

    [Fact]
    public void Media_pubmedia_json_language_index_getpub_query_mediator_lang_and_stream_lowercase_contract()
    {
        Assert.Equal("404", AppConstants.Media.DownloadPermanentFailureHttpFragments.StatusCode404);
        Assert.Equal("Forbidden", AppConstants.Media.DownloadPermanentFailureHttpFragments.Forbidden);

        Assert.Equal("mp3", AppConstants.Media.MediaStreamFormatMp3Lower);
        Assert.Equal("mp4", AppConstants.Media.MediaStreamFormatMp4Lower);

        Assert.Equal("alllangs=1", AppConstants.Media.GetPubQueryAllLangsOn);
        Assert.Equal("langwritten", AppConstants.Media.GetPubQueryParamLangWritten);

        Assert.Equal("booknum", AppConstants.Media.GetPubQueryParamName.BookNum);
        Assert.Equal("issue", AppConstants.Media.GetPubQueryParamName.Issue);
        Assert.Equal("docid", AppConstants.Media.GetPubQueryParamName.DocId);
        Assert.Equal("fileformat", AppConstants.Media.GetPubQueryParamName.FileFormat);
        Assert.Equal("track", AppConstants.Media.GetPubQueryParamName.Track);

        Assert.Equal("lang", AppConstants.Media.MediatorQueryParamName.Lang);

        Assert.Equal("category", AppConstants.Media.PubMediaJson.Category);
        Assert.Equal("name", AppConstants.Media.PubMediaJson.Name);
        Assert.Equal("pubName", AppConstants.Media.PubMediaJson.PubName);
        Assert.Equal("parentPubName", AppConstants.Media.PubMediaJson.ParentPubName);
        Assert.Equal("formattedDate", AppConstants.Media.PubMediaJson.FormattedDate);
        Assert.Equal("file", AppConstants.Media.PubMediaJson.File);
        Assert.Equal("url", AppConstants.Media.PubMediaJson.Url);
        Assert.Equal("track", AppConstants.Media.PubMediaJson.Track);
        Assert.Equal("title", AppConstants.Media.PubMediaJson.Title);
        Assert.Equal("text", AppConstants.Media.PubMediaJson.Text);
        Assert.Equal("label", AppConstants.Media.PubMediaJson.Label);
        Assert.Equal("parentCategory", AppConstants.Media.PubMediaJson.ParentCategory);
        Assert.Equal("primaryCategory", AppConstants.Media.PubMediaJson.PrimaryCategory);
        Assert.Equal("naturalKey", AppConstants.Media.PubMediaJson.NaturalKey);
        Assert.Equal("availableLanguages", AppConstants.Media.PubMediaJson.AvailableLanguages);
        Assert.Equal("language", AppConstants.Media.PubMediaJson.Language);
        Assert.Equal("duration", AppConstants.Media.PubMediaJson.Duration);
        Assert.Equal("Name", AppConstants.Media.PubMediaJson.NamePascal);

        Assert.Equal("data", AppConstants.Media.LanguageIndexJson.Data);
        Assert.Equal("symbol", AppConstants.Media.LanguageIndexJson.Symbol);
        Assert.Equal("direction", AppConstants.Media.LanguageIndexJson.Direction);
        Assert.Equal("isSignLanguage", AppConstants.Media.LanguageIndexJson.IsSignLanguage);
    }

    [Fact]
    public void Platform_Android_iOS_Windows_strings_and_win_ui_single_instance_key_contract()
    {
        Assert.Equal("Android", AppConstants.Platform.Android);
        Assert.Equal("iOS", AppConstants.Platform.IOs);
        Assert.Equal("Windows", AppConstants.Platform.Windows);

        Assert.Equal("BibleAlarmInstance", AppConstants.WinUi.SingleInstanceRegistrationKey);
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
        Assert.Equal("bible-alarm-", AppConstants.FilePaths.LogFileNamePattern);
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
