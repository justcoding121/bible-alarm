#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
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
            Log.Debug("TrackSelectionStateManager.HandleBiblePublicationInitialized: Missing language or publication code, returning");
            return;
        }

        Log.Debug("TrackSelectionStateManager.HandleBiblePublicationInitialized: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
            newLanguageCode, newPublicationCode, newSectionCode ?? "(none)");

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionCode = newSectionCode;

        // Derive from CurrentSchedule (single source of truth)
        // currentSchedule is already declared above
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
            lastCurrent = current;
        }

        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
            await initialize(newLanguageCode, newPublicationCode, newSectionCode);
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
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
        // currentSchedule is already declared above
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
            lastCurrent = current;
        }

        // If language, publication code, or section number changed, repopulate tracks
        if (needsRepopulation && initComplete)
        {
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                await initialize(newLanguageCode, newPublicationCode, newSectionCode);
                await Task.Delay(100);
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
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

        // Derive from CurrentSchedule (single source of truth)
        // currentSchedule is already declared above
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
        }
        else
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
        }
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

        // Derive from CurrentSchedule (single source of truth)
        // currentSchedule is already declared above
        if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode))
        {
            // Create BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = currentSchedule.BiblePublicationLanguageCode,
                PublicationCode = currentSchedule.BiblePublicationCode ?? string.Empty,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = currentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero
            };
        }
        else
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionCode = newSectionCode,
                TrackNumber = currentSchedule?.BiblePublicationTrackNumber ?? 1
            };
        }
        lastCurrent = current;
    }

    public void SetInitComplete(bool value) => initComplete = value;
}

