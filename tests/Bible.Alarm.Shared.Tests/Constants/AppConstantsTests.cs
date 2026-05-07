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
        Assert.Equal("ReviewLastAttemptAtUtc", AppConstants.GeneralSettingsKeys.ReviewLastAttemptAtUtc);
        Assert.Equal("MediaIndexVersion", AppConstants.GeneralSettingsKeys.MediaIndexVersion);
    }

    [Fact]
    public void Logging_serilog_templates_environment_and_fallback_tokens()
    {
        Assert.Contains("{Timestamp:", AppConstants.Logging.ConsoleOutputTemplate, StringComparison.Ordinal);
        Assert.Contains("{Level:", AppConstants.Logging.ConsoleOutputTemplate, StringComparison.Ordinal);
        Assert.Contains("{Timestamp:", AppConstants.Logging.FileOutputTemplate, StringComparison.Ordinal);
        Assert.EndsWith("{Exception}", AppConstants.Logging.FileOutputTemplate, StringComparison.Ordinal);

        Assert.Equal("DEBUG", AppConstants.Logging.DebugEnvironment);
        Assert.Equal("AssemblyVersionNotFound", AppConstants.Logging.AssemblyVersionFallback);
        Assert.Equal("Unknown error", AppConstants.Logging.UnknownErrorFallback);

        Assert.Contains("alarm", AppConstants.Logging.AlarmDiagnostics.RingingAlarmFailed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("review", AppConstants.Logging.AlarmDiagnostics.ReviewRequestedFailed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Logging_process_schedule_lookup_exact_alarm_disposal_enable_and_state_templates()
    {
        Assert.Equal("Unobserved task exception.", AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        Assert.Contains("{IsTerminating}", AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionIsTerminating);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ProcessDiagnosticsLog.AlarmTriggeredWhilePlaybackActiveStoppingForNewAlarm);

        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleLookupDiagnosticsLog.NotFoundStoppingForegroundService);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleLookupDiagnosticsLog.NotFoundForDeletion);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleLookupDiagnosticsLog.LoadExistingScheduleNotFoundInDatabase);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleLookupDiagnosticsLog.SetScheduleIdNotFoundInState);

        Assert.Contains("SCHEDULE_EXACT_ALARM", AppConstants.Logging.AndroidExactAlarmSchedulingLog.SecurityExceptionSchedulingAlarmForSchedule);
        Assert.Contains("SCHEDULE_EXACT_ALARM", AppConstants.Logging.AndroidExactAlarmSchedulingLog.SecurityExceptionUpdatingSchedule);

        Assert.False(string.IsNullOrWhiteSpace(AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal));

        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableNotificationDeniedTapToPlay);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableIosRemindersPermissionDenied);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleEnableDiagnosticsLog.PermissionRequestTimeoutForSchedule);

        var scheduleStateMsgs = new[]
        {
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidScheduleNotificationEnabledCheckingPermissionBeforeReminder,
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidNotificationPermissionGrantedForSchedule,
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.AndroidScheduleNotificationDisabledNoPermissionCheckNeeded,
            AppConstants.Logging.ScheduleStateServiceDiagnosticsLog.RequestingIosNotificationPermissionForSchedule,
        };
        Assert.All(scheduleStateMsgs, m => Assert.Contains("{ScheduleId}", m));
    }

    [Fact]
    public void Logging_playback_main_activity_battery_bootstrap_scheduler_database_seed_spot_checks()
    {
        Assert.Contains("{ScheduleId}", AppConstants.Logging.SchedulePlaybackServiceDiagnosticsLog.PlayScheduleAsyncPlayLockAlreadyHeldSkipping);
        Assert.Contains("{Timeout}", AppConstants.Logging.SchedulePlaybackServiceDiagnosticsLog.PlayScheduleAsyncOverallTimeoutReleasingPlayLockBackgroundContinues);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.SchedulePlaybackServiceDiagnosticsLog.PlaybackCancelledForSchedule);

        Assert.Equal(
            "Error setting up background tasks",
            AppConstants.Logging.MainActivityBackgroundTaskHelperDiagnosticsLog.ErrorSettingUpBackgroundTasks);

        Assert.Contains(
            "battery",
            AppConstants.Logging.BatteryOptimizationDiagnosticsLog.ErrorMarkingBatteryOptimizationModalAsShown,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("{Value}", AppConstants.Logging.BootstrapReadyManagerDiagnosticsLog.IsBootstrapReadyChangedTo);

        Assert.Contains(
            "bootstrap",
            AppConstants.Logging.SchedulerDiagnosticsLog.BootstrapNotCompletedWaitingForBootstrap,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "An error happenned inside cleanup task.",
            AppConstants.Logging.SchedulerDiagnosticsLog.ErrorInsideCleanupTask);

        Assert.Contains("{ScheduleId}", AppConstants.Logging.DatabaseSeedDiagnosticsLog.SeededDefaultAlarmSchedule);
        Assert.Contains("{Name}", AppConstants.Logging.DatabaseSeedDiagnosticsLog.SeededDefaultAlarmSchedule);
    }

    [Fact]
    public void Logging_schedule_initialization_command_validation_and_database_version_templates()
    {
        Assert.Contains("{LanguageCode}", AppConstants.Logging.ScheduleInitializationDiagnosticsLog.InitializeNewScheduleMappedSampleMusicCodes);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleInitializationDiagnosticsLog.LoadExistingScheduleLoadingFromDatabase);

        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleCommandDiagnosticsLog.CancelCommandCancelButtonClicked);
        Assert.Contains("{ScheduleId}", AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveCommandSaveButtonClicked);

        Assert.Equal(
            "Validation failed: No days of week selected",
            AppConstants.Logging.ScheduleValidationServiceDiagnosticsLog.ValidationFailedNoDaysOfWeekSelected);

        Assert.Contains("{StoredVersion}", AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.VersionMismatchStoredVersusCurrent);
        Assert.Contains("{CurrentVersion}", AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.VersionMismatchStoredVersusCurrent);
        Assert.Contains("{Version}", AppConstants.Logging.ScheduleDatabaseVersionServiceDiagnosticsLog.SavedScheduleDatabaseVersionToPreferences);
    }

    [Fact]
    public void Logging_schedule_save_prep_and_music_cascade_handler_templates()
    {
        Assert.Contains("{IsNewSchedule}", AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareModelForSaveStarting);
        Assert.Contains("nwt", AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.SaveAsyncBiblePublicationEmptyPublicationCodeDefaultingNwt, StringComparison.Ordinal);
        Assert.Contains("{PublicationCode}", AppConstants.Logging.ScheduleSaveServiceDiagnosticsLog.PrepareScheduleStateItemFinalBeforeDispatch);

        Assert.StartsWith("MusicCascadeHandler:", AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncCurrentScheduleNullExiting, StringComparison.Ordinal);
        Assert.Contains("{PublicationCode}", AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.HandleAsyncPublicationSectionTrackMusicEnabled);
        Assert.Equal("MusicCascadeHandler: Error during cascade", AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ErrorDuringCascade);
    }

    [Fact]
    public void Logging_music_cascade_modal_publication_section_and_cycle_guard_templates()
    {
        Assert.Contains("{CurrentPubCount}", AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.RefreshModalCountsIfNeededCurrentVsNew);
        Assert.Equal(
            "MusicCascadeHandler: Modal counts unchanged, skipping dispatch",
            AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ModalCountsUnchangedSkippingDispatch);

        Assert.Contains("{PublicationCode}", AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationCascade);
        Assert.Contains("{LanguageCode}", AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.PublicationCascade);

        var cycleGuard = AppConstants.Logging.MusicCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatchCycle;
        Assert.Contains("{PublicationCode}", cycleGuard);
        Assert.Contains("{SectionCode}", cycleGuard);
        Assert.Contains("{TrackCode}", cycleGuard);
    }

    [Fact]
    public void Logging_category_auto_populate_and_bible_publication_cascade_templates()
    {
        Assert.StartsWith(
            "CategorySelectionAutoPopulateHandler:",
            AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.StartingAutoPopulation,
            StringComparison.Ordinal);
        Assert.Contains("{CategoryName}", AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.NetworkErrorDuringAutoPopulation);

        var summary = AppConstants.Logging.CategorySelectionAutoPopulateHandlerDiagnosticsLog.AutoPopulatedSummary;
        Assert.Contains("{LanguageCode}", summary);
        Assert.Contains("{PublicationCode}", summary);
        Assert.Contains("{TrackCode}", summary);

        Assert.Equal(
            "BiblePublicationCascadeHandler: Error during cascade",
            AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ErrorDuringCascade);
        Assert.Contains("{LanguageCode}", AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.LanguageCascade);
        Assert.Contains("{PublicationCode}", AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.PublicationCascade);

        var bibleUnchanged = AppConstants.Logging.BiblePublicationCascadeHandlerDiagnosticsLog.ValuesUnchangedSkippingDispatch;
        Assert.Contains("{PublicationCode}", bibleUnchanged);
        Assert.Contains("{SectionCode}", bibleUnchanged);
        Assert.Contains("{TrackCode}", bibleUnchanged);
    }

    [Fact]
    public void Logging_bible_publication_selection_item_selector_templates()
    {
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackStarting);
        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackStarting);
        Assert.Contains(
            "{SectionCount}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackFoundSectionsSectionedFlow);

        Assert.Contains(
            "{SectionCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackSectionedResult);
        Assert.Contains(
            "{TrackTitle}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetSectionAndTrackSectionedResult);

        Assert.Contains(
            "{LanguageCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationSectionTrackLangStarting);
        Assert.Contains(
            "{CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.NoPublicationFoundForLanguageCategory);

        Assert.Contains(
            "{TrackCode}",
            AppConstants.Logging.BiblePublicationSelectionItemSelectorDiagnosticsLog.GetPublicationNonSectionedResult);
    }

    [Fact]
    public void Logging_bible_publication_selection_data_provider_and_section_track_resolver_templates()
    {
        Assert.Contains(
            "{LanguageCount}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulateLanguagesLoaded);
        Assert.Contains(
            "{Attempt}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsAllExpectedCatalogedOnAttempt);
        Assert.Contains(
            "{CategoryName}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.PopulatePublicationsNoProgressBetweenRetriesStopping);

        Assert.Contains(
            "{SectionCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationDispatchingTrackSelected);
        Assert.Contains(
            "{TrackCode}",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.DispatchDefaultPublicationDispatchingTrackSelected);

        Assert.StartsWith(
            "BibleSelectionDataProvider:",
            AppConstants.Logging.BiblePublicationSelectionDataProviderDiagnosticsLog.ErrorDispatchingDefaultPublicationSelection,
            StringComparison.Ordinal);

        Assert.Contains(
            "{SectionCodeKey}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstSectionFirstSectionCodeKey);
        Assert.Contains(
            "{PublicationCode}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.SectionFoundButNoTracks);
        Assert.Contains(
            "{HasService}",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstTrackNonSectionedStarting);

        Assert.Equal(
            "GetFirstTrackForNonSectionedAsync: biblePublicationService is null, returning empty result",
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.BiblePublicationServiceNullReturningEmpty);
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
