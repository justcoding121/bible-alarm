#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for BiblePublicationTrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionStateManager
{
    private BiblePublicationSchedule? current;
    private BiblePublicationSchedule? lastCurrent;
    private bool initComplete;

    // Track last language, publication code, and section number to detect changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private string? lastSectionCode;

    public BiblePublicationSchedule? Current => current;
    public bool InitComplete => initComplete;
    public string? LastLanguageCode => lastLanguageCode;
    public string? LastPublicationCode => lastPublicationCode;
    public string? LastSectionCode => lastSectionCode;

    public void HandleBiblePublicationInitialized(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<string, string, string?, Task> initialize)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationCode;
        var newSectionCode = currentSchedule.BiblePublicationSectionCode;

        // For non-sectioned publications, sectionCode is 0 or null - that's valid
        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            Log.Debug(AppConstants.Logging.TrackSelectionStateManagerDiagnosticsLog.HandleInitializedMissingLanguageOrPublicationReturning);
            return;
        }

        Log.Debug(AppConstants.Logging.TrackSelectionStateManagerDiagnosticsLog.HandleInitializedLanguagePublicationSection,
            newLanguageCode, newPublicationCode, newSectionCode ?? "(none)");

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;

        // Derive from CurrentSchedule (single source of truth)
        current = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode,
            PublicationCode = newPublicationCode,
            SectionCode = newSectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };
        lastCurrent = current;

        initComplete = true;
        Task.Run(async () =>
        {
            // Note: Do NOT set setBusy(true) here - the modal controls the busy state via ModalScrollHelper
            await initialize(newLanguageCode, newPublicationCode, newSectionCode);
            // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
        });
    }

    public void HandleBiblePublicationChanged(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<string, string, string?, Task> initialize,
        Action setSelectedTrack)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationCode;
        var newSectionCode = currentSchedule.BiblePublicationSectionCode;

        // For non-sectioned publications, sectionCode is 0 or null - that's valid
        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // Check if language, publication code, or section code changed
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var sectionCodeChanged = !string.Equals(lastSectionCode, newSectionCode, StringComparison.OrdinalIgnoreCase);
        var needsRepopulation = languageChanged || publicationCodeChanged || sectionCodeChanged;

        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;

        // Derive from CurrentSchedule (single source of truth)
        current = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode,
            PublicationCode = newPublicationCode,
            SectionCode = newSectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };
        lastCurrent = current;

        // If language, publication code, or section number changed, repopulate tracks
        if (needsRepopulation && initComplete)
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                    await initialize(newLanguageCode, newPublicationCode, newSectionCode);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, AppConstants.Logging.TrackSelectionStateManagerDiagnosticsLog.ErrorInHandleBiblePublicationChangedDuringTrackPopulation);
                }
                finally
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
                }
            });
        }
        else
        {
            // Update selected track when state changes
            MainThread.BeginInvokeOnMainThread(setSelectedTrack);
        }
    }

    public void UpdateFromState(IState<ApplicationState> state)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationCode;
        var newSectionCode = currentSchedule.BiblePublicationSectionCode;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode) || string.IsNullOrWhiteSpace(newSectionCode))
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;

        // Derive from CurrentSchedule (single source of truth); guards above ensure language and publication are non-empty.
        current = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode,
            PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
            SectionCode = newSectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };

        lastCurrent = current;
    }

    /// <summary>
    /// Updates state from CurrentSchedule, allowing section number 0 for non-sectioned publications.
    /// </summary>
    public void UpdateFromStateForNonSectioned(IState<ApplicationState> state)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationCode;
        var newSectionCode = currentSchedule.BiblePublicationSectionCode;

        // For non-sectioned publications, sectionCode is 0 or null - that's valid
        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;

        // Derive from CurrentSchedule (single source of truth); guards above ensure language and publication are non-empty.
        current = new BiblePublicationSchedule
        {
            LanguageCode = newLanguageCode,
            PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
            SectionCode = newSectionCode,
            TrackCode = currentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
        };

        lastCurrent = current;
    }

    public void SetInitComplete(bool value) => initComplete = value;
}

