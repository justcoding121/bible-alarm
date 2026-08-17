#nullable enable
using System;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using BiblePublicationTrackSelectedAction = Bible.Alarm.Stores.Actions.BiblePublications.TrackSelectedAction;
using IDispatcher = Fluxor.IDispatcher;
using MusicTrackSelectedAction = Bible.Alarm.Stores.Actions.Music.TrackSelectedAction;

namespace Bible.Alarm.Stores.Effects.Services;

public sealed class TrackSelectionSyncHandler
{
    private const string LogNullDisplay = "(null)";
    private const string LogNotNullDisplay = "not null";

    private readonly IState<ApplicationState>? state;

    public TrackSelectionSyncHandler(IState<ApplicationState>? state = null)
    {
        this.state = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>();
    }

    /// <summary>
    /// Effect: Sync CurrentMusic to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentMusic, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    public async Task HandleTrackSelected(MusicTrackSelectedAction action, IDispatcher dispatcher)
    {
        try
        {
            LogTrackSelectedStart(action);

            var currentState = state?.Value;
            if (!CanSyncTrackSelection(currentState, action))
            {
                return;
            }

            var currentSchedule = currentState!.CurrentSchedule!;
            var languageCodeChanged = HasLanguageCodeChanged(currentSchedule, action.CurrentMusic!);

            if (!ShouldSyncMusic(currentSchedule, action.CurrentMusic!, languageCodeChanged))
            {
                return;
            }

            if (languageCodeChanged)
            {
                Log.Information(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedMusicLanguageChangedSyncing,
                    currentSchedule.MusicLanguageCode, action.CurrentMusic.LanguageCode);
            }

            var updatedSchedule = await CreateUpdatedScheduleFromTrackSelectionAsync(currentSchedule, action, languageCodeChanged);
            DispatchTrackUpdateAction(dispatcher, updatedSchedule, currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.ErrorSyncingCurrentMusicToCurrentSchedule);
        }
    }

    /// <summary>
    /// Effect: Sync CurrentBiblePublicationSchedule to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentBiblePublicationSchedule, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    public async Task HandleTrackSelected(BiblePublicationTrackSelectedAction action, IDispatcher dispatcher)
    {
        try
        {
            var actionPub = action.CurrentBiblePublicationSchedule;
            Log.Information(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedBiblePublicationReceivedAction,
                actionPub != null ? LogNotNullDisplay : "null",
                actionPub?.TrackCode ?? LogNullDisplay,
                actionPub?.TrackTitle ?? LogNullDisplay,
                actionPub?.SectionCode ?? (actionPub?.SectionCode?.ToString() ?? LogNullDisplay),
                actionPub?.PublicationCode ?? LogNullDisplay);

            var currentState = state?.Value;
            if (currentState?.CurrentSchedule == null || action.CurrentBiblePublicationSchedule == null)
            {
                Log.Warning(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedBiblePublicationCurrentScheduleOrPubNull);
                return;
            }

            var currentSchedule = currentState.CurrentSchedule;
            var biblePub = action.CurrentBiblePublicationSchedule;
            var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
            var actionSectionCode = biblePub.SectionCode;

            // The reducer OnBiblePublicationTrackSelected now updates CurrentSchedule synchronously,
            // so we should check if CurrentSchedule already has the action's values.
            // If everything is already in sync, skip the redundant dispatch to avoid extra state updates.
            var alreadyInSync =
                string.Equals(currentSchedule.BiblePublicationLanguageCode, biblePub.LanguageCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(currentSchedule.BiblePublicationCode, biblePub.PublicationCode, StringComparison.OrdinalIgnoreCase) &&
                SectionCodeHelper.CodeEquals(currentSectionCode, actionSectionCode) &&
                CodeComparisonHelper.Equals(currentSchedule.BiblePublicationTrackCode, biblePub.TrackCode);
            
            if (alreadyInSync)
            {
                Log.Debug(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedBiblePublicationAlreadyInSync);
                return;
            }

            // Detect if language or publication changed - if so, we should NOT preserve old names
            // Language code can ONLY be changed by:
            // 1. Category change (will default language to E)
            // 2. By user explicitly changing the language
            // 
            // IMPORTANT: When user selects a publication with no language (null), the current language stays the same.
            // The publication's language code (null) will be used in queries, but the schedule's language code doesn't change.
            var languageChanged = !string.IsNullOrEmpty(biblePub.LanguageCode) &&
                                  !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode) &&
                                  !string.Equals(biblePub.LanguageCode, currentSchedule.BiblePublicationLanguageCode, StringComparison.OrdinalIgnoreCase);

            var publicationChanged = !string.IsNullOrEmpty(biblePub.PublicationCode) &&
                                     !string.Equals(biblePub.PublicationCode, currentSchedule.BiblePublicationCode, StringComparison.OrdinalIgnoreCase);

            // Create updated schedule with Bible publication properties
            var updatedSchedule = ApplyBiblePublicationTrackSelection(
                currentSchedule,
                biblePub,
                languageChanged,
                publicationChanged,
                currentSectionCode,
                actionSectionCode);

            Log.Information(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedBiblePublicationDispatchingUpdate,
                updatedSchedule.Id, updatedSchedule.BiblePublicationTrackCode, updatedSchedule.BiblePublicationTrackTitle, updatedSchedule.BiblePublicationSectionCode);
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.ErrorSyncingCurrentBiblePublicationScheduleToCurrentSchedule);
        }
    }

    private static ScheduleStateItem ApplyBiblePublicationTrackSelection(
        ScheduleStateItem currentSchedule,
        BiblePublicationStateItem biblePub,
        bool languageChanged,
        bool publicationChanged,
        string? currentSectionCode,
        string? actionSectionCode)
    {
        var updatedSchedule = currentSchedule.DeepClone();
        updatedSchedule.BiblePublicationScheduleId = biblePub.Id > 0 ? biblePub.Id : currentSchedule.BiblePublicationScheduleId;

        if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationCategoryName))
        {
            if (!string.IsNullOrWhiteSpace(biblePub.CategoryName))
            {
                updatedSchedule.BiblePublicationCategoryId = biblePub.CategoryId;
                updatedSchedule.BiblePublicationCategoryName = biblePub.CategoryName;
                Log.Debug(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.SetCategoryFromBiblePublicationStateItemWasNull,
                    biblePub.CategoryName);
            }
            else
            {
                Log.Error(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.CategoryNullInBothScheduleAndBiblePublicationStateItem);
            }
        }
        else if (!updatedSchedule.BiblePublicationCategoryId.HasValue && biblePub.CategoryId.HasValue)
        {
            updatedSchedule.BiblePublicationCategoryId = biblePub.CategoryId;
        }

        if (languageChanged)
        {
            updatedSchedule.BiblePublicationLanguageCode = biblePub.LanguageCode;
            updatedSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName)
                ? biblePub.LanguageName
                : updatedSchedule.BiblePublicationLanguageName ?? string.Empty;
            updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection)
                ? biblePub.LanguageDirection
                : updatedSchedule.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
        }

        updatedSchedule.BiblePublicationCode = biblePub.PublicationCode;
        updatedSchedule.BiblePublicationSectionCode = actionSectionCode;
        updatedSchedule.BiblePublicationTrackCode = biblePub.TrackCode;

        if (languageChanged || publicationChanged ||
            !Bible.Alarm.Shared.Helpers.SectionCodeHelper.CodeEquals(currentSectionCode, actionSectionCode) ||
            currentSchedule.BiblePublicationTrackCode != biblePub.TrackCode)
        {
            updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;
        }

        ApplyBiblePublicationDisplayNames(updatedSchedule, currentSchedule, biblePub, languageChanged, publicationChanged, actionSectionCode);

        return updatedSchedule;
    }

    private static void ApplyBiblePublicationDisplayNames(
        ScheduleStateItem updatedSchedule,
        ScheduleStateItem currentSchedule,
        BiblePublicationStateItem biblePub,
        bool languageChanged,
        bool publicationChanged,
        string? actionSectionCode)
    {
        if (languageChanged)
        {
            ApplyBiblePublicationDisplayNamesForLanguageChanged(updatedSchedule, biblePub);
            return;
        }

        if (publicationChanged)
        {
            ApplyBiblePublicationDisplayNamesForPublicationChanged(
                updatedSchedule, currentSchedule, biblePub, actionSectionCode);
            return;
        }

        ApplyBiblePublicationDisplayNamesWhenLanguageAndPublicationUnchanged(
            updatedSchedule, currentSchedule, biblePub);
    }

    private static void ApplyBiblePublicationDisplayNamesWhenLanguageAndPublicationUnchanged(
        ScheduleStateItem updatedSchedule,
        ScheduleStateItem currentSchedule,
        BiblePublicationStateItem biblePub)
    {
        updatedSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName)
            ? biblePub.LanguageName
            : currentSchedule.BiblePublicationLanguageName;
        updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection)
            ? biblePub.LanguageDirection
            : currentSchedule.BiblePublicationLanguageDirection;
        updatedSchedule.BiblePublicationName = !string.IsNullOrEmpty(biblePub.PublicationName)
            ? biblePub.PublicationName
            : currentSchedule.BiblePublicationName;
        updatedSchedule.BiblePublicationSectionName = !string.IsNullOrEmpty(biblePub.SectionName)
            ? biblePub.SectionName
            : currentSchedule.BiblePublicationSectionName;
        updatedSchedule.BiblePublicationTrackTitle = !string.IsNullOrEmpty(biblePub.TrackTitle)
            ? biblePub.TrackTitle
            : currentSchedule.BiblePublicationTrackTitle;
    }

    private static void ApplyBiblePublicationDisplayNamesForLanguageChanged(
        ScheduleStateItem updatedSchedule,
        BiblePublicationStateItem biblePub)
    {
        updatedSchedule.BiblePublicationName = biblePub.PublicationName ?? string.Empty;
        updatedSchedule.BiblePublicationSectionName = biblePub.SectionName ?? string.Empty;
        updatedSchedule.BiblePublicationTrackTitle = biblePub.TrackTitle ?? string.Empty;
    }

    private static void ApplyBiblePublicationDisplayNamesForPublicationChanged(
        ScheduleStateItem updatedSchedule,
        ScheduleStateItem currentSchedule,
        BiblePublicationStateItem biblePub,
        string? actionSectionCode)
    {
        updatedSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName)
            ? biblePub.LanguageName
            : currentSchedule.BiblePublicationLanguageName;
        updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection)
            ? biblePub.LanguageDirection
            : currentSchedule.BiblePublicationLanguageDirection;
        updatedSchedule.BiblePublicationName = biblePub.PublicationName ?? string.Empty;
        updatedSchedule.BiblePublicationSectionName = biblePub.SectionName ?? string.Empty;
        updatedSchedule.BiblePublicationTrackTitle = biblePub.TrackTitle ?? string.Empty;

        if (string.IsNullOrEmpty(updatedSchedule.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(actionSectionCode))
        {
            Log.Warning(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.SectionNameEmptyAfterPublicationChangeSectionCodeSet,
                actionSectionCode, biblePub.PublicationCode);
        }

        if (string.IsNullOrEmpty(updatedSchedule.BiblePublicationTrackTitle) && !string.IsNullOrWhiteSpace(biblePub.TrackCode))
        {
            Log.Warning(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.TrackTitleEmptyAfterPublicationChangeTrackCodeValid,
                biblePub.TrackCode, biblePub.PublicationCode);
        }
    }

    private static void LogTrackSelectedStart(MusicTrackSelectedAction action)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal
        Log.Information(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedReceivedActionMusic,
            action.CurrentMusic != null ? LogNotNullDisplay : "null",
            action.CurrentMusic?.LanguageCode ?? "null (melody)",
            action.CurrentMusic?.PublicationCode ?? "null",
            action.CurrentMusic?.TrackCode ?? LogNullDisplay);
    }

    private static bool CanSyncTrackSelection(ApplicationState? currentState, MusicTrackSelectedAction action)
    {
        if (currentState?.CurrentSchedule == null || action.CurrentMusic == null)
        {
            Log.Warning(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedCurrentScheduleOrCurrentMusicNull,
                currentState?.CurrentSchedule != null ? LogNotNullDisplay : "null",
                action.CurrentMusic != null ? LogNotNullDisplay : "null");
            return false;
        }
        return true;
    }

    private static bool HasLanguageCodeChanged(ScheduleStateItem currentSchedule, MusicStateItem actionMusic)
    {
        var effectiveNewLanguageCode = !string.IsNullOrWhiteSpace(actionMusic.LanguageCode)
            ? actionMusic.LanguageCode
            : (currentSchedule.MusicLanguageCode ?? Bible.Alarm.Shared.Constants.AppConstants.Media.DefaultLanguageCode);
        return currentSchedule.MusicLanguageCode != effectiveNewLanguageCode;
    }

    private static bool ShouldSyncMusic(ScheduleStateItem currentSchedule, MusicStateItem actionMusic, bool languageCodeChanged)
    {
        Log.Debug(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedCurrentScheduleIdsDebug,
            currentSchedule.Id, currentSchedule.MusicId, actionMusic.Id);

        // Allow syncing if:
        // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
        // 2. Action Id matches current schedule's MusicId - same schedule, sync
        // 3. Language code changed (e.g., Melodies -> Vocals) - always sync to update current schedule
        // Reject only if action has a non-zero ID that doesn't match (different schedule) AND language hasn't changed
        if (actionMusic.Id > 0 &&
            currentSchedule.MusicId.HasValue &&
            actionMusic.Id != currentSchedule.MusicId.Value &&
            !languageCodeChanged)
        {
            Log.Warning(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedDifferentMusicId,
                currentSchedule.MusicId.Value, actionMusic.Id);
            return false;
        }

        Log.Debug(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedSyncingAllowed,
            actionMusic.Id, currentSchedule.MusicId);
        return true;
    }

    private static async Task<ScheduleStateItem> CreateUpdatedScheduleFromTrackSelectionAsync(ScheduleStateItem currentSchedule, MusicTrackSelectedAction action, bool languageCodeChanged)
    {
        var updatedSchedule = CloneBasicScheduleProperties(currentSchedule);
        PreserveBiblePublicationProperties(updatedSchedule, currentSchedule);
        UpdateMusicProperties(updatedSchedule, currentSchedule, action.CurrentMusic!, languageCodeChanged);
        await SetMusicDisplayNamesAsync(updatedSchedule, action.CurrentMusic!);
        return updatedSchedule;
    }

    private static ScheduleStateItem CloneBasicScheduleProperties(ScheduleStateItem currentSchedule)
    {
        return new ScheduleStateItem
        {
            Id = currentSchedule.Id,
            Name = currentSchedule.Name,
            IsEnabled = currentSchedule.IsEnabled,
            Hour = currentSchedule.Hour,
            Minute = currentSchedule.Minute,
            Second = currentSchedule.Second,
            DaysOfWeek = currentSchedule.DaysOfWeek,
            NotificationEnabled = currentSchedule.NotificationEnabled,
            MusicEnabled = currentSchedule.MusicEnabled,
            SnoozeMinutes = currentSchedule.SnoozeMinutes,
            NumberOfTracksToPlay = currentSchedule.NumberOfTracksToPlay,
            AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart,
            CurrentPlayItem = currentSchedule.CurrentPlayItem,
            LatestAlarmNotificationId = currentSchedule.LatestAlarmNotificationId
        };
    }

    private static void PreserveBiblePublicationProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule)
    {
        updatedSchedule.BiblePublicationScheduleId = currentSchedule.BiblePublicationScheduleId;
        updatedSchedule.BiblePublicationLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        updatedSchedule.BiblePublicationCode = currentSchedule.BiblePublicationCode;
        updatedSchedule.BiblePublicationSectionCode = currentSchedule.BiblePublicationSectionCode;
        updatedSchedule.BiblePublicationTrackCode = currentSchedule.BiblePublicationTrackCode;
        updatedSchedule.BiblePublicationFinishedDuration = currentSchedule.BiblePublicationFinishedDuration;
        updatedSchedule.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
        updatedSchedule.BiblePublicationName = currentSchedule.BiblePublicationName;
        updatedSchedule.BiblePublicationSectionName = currentSchedule.BiblePublicationSectionName;
    }

    private static void UpdateMusicProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule, MusicStateItem actionMusic, bool languageCodeChanged)
    {
        ApplyMusicIdentityFromTrackSelection(updatedSchedule, currentSchedule, actionMusic, languageCodeChanged);
        updatedSchedule.MusicPublicationCode = actionMusic.PublicationCode;
        var useScheduleLanguage = string.IsNullOrWhiteSpace(actionMusic.LanguageCode);
        updatedSchedule.MusicLanguageCode = useScheduleLanguage
            ? currentSchedule.MusicLanguageCode ?? Bible.Alarm.Shared.Constants.AppConstants.Media.DefaultLanguageCode
            : actionMusic.LanguageCode;

        updatedSchedule.MusicSectionCode = actionMusic.SectionCode;
        updatedSchedule.MusicTrackCode = actionMusic.TrackCode;
        updatedSchedule.MusicRepeat = actionMusic.Repeat;

        ApplyMusicLanguageDisplayFields(updatedSchedule, currentSchedule, actionMusic, useScheduleLanguage);
        updatedSchedule.MusicPublicationName = currentSchedule.MusicPublicationName;
        updatedSchedule.MusicTrackName = currentSchedule.MusicTrackName;
    }

    private static void ApplyMusicIdentityFromTrackSelection(
        ScheduleStateItem updatedSchedule,
        ScheduleStateItem currentSchedule,
        MusicStateItem actionMusic,
        bool languageCodeChanged)
    {
        int? musicIdFromAction = actionMusic.Id > 0 ? actionMusic.Id : null;

        if (languageCodeChanged || actionMusic.Id == 0)
        {
            updatedSchedule.MusicId = musicIdFromAction;
        }
        else
        {
            updatedSchedule.MusicId = currentSchedule.MusicId;
        }
    }

    private static void ApplyMusicLanguageDisplayFields(
        ScheduleStateItem updatedSchedule,
        ScheduleStateItem currentSchedule,
        MusicStateItem actionMusic,
        bool useScheduleLanguage)
    {
        if (useScheduleLanguage)
        {
            updatedSchedule.MusicLanguageName = currentSchedule.MusicLanguageName;
            updatedSchedule.MusicLanguageDirection = currentSchedule.MusicLanguageDirection;
            return;
        }

        updatedSchedule.MusicLanguageName = actionMusic.LanguageName ?? currentSchedule.MusicLanguageName;
        updatedSchedule.MusicLanguageDirection = actionMusic.LanguageDirection ?? currentSchedule.MusicLanguageDirection;
    }

    private static async Task SetMusicDisplayNamesAsync(ScheduleStateItem updatedSchedule, MusicStateItem actionMusic)
    {
        // Use display names from the action when present. For melody (no LanguageName), keep values set in UpdateMusicProperties (e.g. MY display).
        if (!string.IsNullOrEmpty(actionMusic.LanguageName))
            updatedSchedule.MusicLanguageName = actionMusic.LanguageName;
        if (!string.IsNullOrEmpty(actionMusic.LanguageDirection))
            updatedSchedule.MusicLanguageDirection = actionMusic.LanguageDirection;
        updatedSchedule.MusicPublicationName = actionMusic.PublicationName;
        updatedSchedule.MusicSectionName = actionMusic.SectionName;
        updatedSchedule.MusicTrackName = actionMusic.TrackName;

        await TryEnrichVocalLanguageDisplayFromCatalogAsync(updatedSchedule, actionMusic);

        Log.Debug(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedUsingDisplayNamesFromAction,
            updatedSchedule.MusicLanguageName ?? "null",
            updatedSchedule.MusicPublicationName ?? "null",
            updatedSchedule.MusicSectionName ?? "null",
            updatedSchedule.MusicTrackName ?? "null");
    }

    private static async Task TryEnrichVocalLanguageDisplayFromCatalogAsync(ScheduleStateItem updatedSchedule, MusicStateItem actionMusic)
    {
        if (!string.IsNullOrWhiteSpace(updatedSchedule.MusicLanguageName) || string.IsNullOrWhiteSpace(actionMusic.LanguageCode))
        {
            return;
        }

        var mediaService = ServiceProviderManager.GetService<IMediaService>();
        var languageNameService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageNameService>();
        try
        {
            if (mediaService != null)
            {
                var languages = await mediaService.GetVocalMusicLanguages();
                if (languages.TryGetValue(actionMusic.LanguageCode, out var language))
                {
                    updatedSchedule.MusicLanguageDirection = language.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
                    updatedSchedule.MusicLanguageName = languageNameService != null
                        ? await languageNameService.GetNameAsync(language.Id, AppConstants.Media.DefaultLanguageCode) ?? actionMusic.LanguageCode
                        : actionMusic.LanguageCode;
                }
                else
                {
                    updatedSchedule.MusicLanguageName = actionMusic.LanguageCode;
                    updatedSchedule.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                }
            }
            else
            {
                updatedSchedule.MusicLanguageName = actionMusic.LanguageCode;
                updatedSchedule.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedCouldNotResolveVocalLanguageDisplayName, actionMusic.LanguageCode);
            updatedSchedule.MusicLanguageName = actionMusic.LanguageCode;
            updatedSchedule.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
        }
    }

    private static void DispatchTrackUpdateAction(IDispatcher dispatcher, ScheduleStateItem updatedSchedule, int scheduleId)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal
        Log.Information(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedDispatchingUpdateScheduleFromViewModelMusic,
            updatedSchedule.Id,
            updatedSchedule.MusicLanguageCode ?? "null (melody)",
            updatedSchedule.MusicLanguageName ?? "null",
            updatedSchedule.MusicPublicationCode ?? "null",
            updatedSchedule.MusicPublicationName ?? "null",
            updatedSchedule.MusicTrackCode,
            updatedSchedule.MusicTrackName ?? "null");
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));

        Log.Debug(AppConstants.Logging.TrackSelectionSyncHandlerDiagnosticsLog.HandleTrackSelectedSyncedCurrentMusicToSchedule,
            scheduleId);
    }
}

