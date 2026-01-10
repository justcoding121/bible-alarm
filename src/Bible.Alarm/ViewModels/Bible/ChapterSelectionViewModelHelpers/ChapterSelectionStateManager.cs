#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Bible.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for TrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionStateManager(IMapper mapper)
{
    private BiblePublicationSchedule? current;
    private BiblePublicationSchedule? lastCurrent;
    private bool initComplete;

    // Track last language, publication code, and section number to detect changes
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private int? lastSectionNumber;

    public BiblePublicationSchedule? Current => current;
    public bool InitComplete => initComplete;
    public string? LastLanguageCode => lastLanguageCode;
    public string? LastPublicationCode => lastPublicationCode;
    public int? LastSectionNumber => lastSectionNumber;

    public void HandleBiblePublicationInitialized(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<string, string, int, Task> initialize)
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
        var newPublicationCode = currentSchedule.BiblePublicationPublicationCode;
        var newSectionNumber = currentSchedule.BiblePublicationSectionNumber;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode) || !newSectionNumber.HasValue)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionNumber = newSectionNumber.Value;

        // Update current if we have CurrentBiblePublicationSchedule
        if (stateValue.CurrentBiblePublicationSchedule != null)
        {
            current = mapper.Map<BiblePublicationSchedule>(stateValue.CurrentBiblePublicationSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionNumber = newSectionNumber.Value,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
            };
            lastCurrent = current;
        }

        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
            await initialize(newLanguageCode, newPublicationCode, newSectionNumber.Value);
            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
        });
    }

    public void HandleBiblePublicationChanged(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<string, string, int, Task> initialize,
        Action setSelectedTrack)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationPublicationCode;
        var newSectionNumber = currentSchedule.BiblePublicationSectionNumber;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode) || !newSectionNumber.HasValue)
        {
            return;
        }

        // Check if language, publication code, or section number changed
        var languageChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var sectionNumberChanged = lastSectionNumber != newSectionNumber.Value;
        var needsRepopulation = languageChanged || publicationCodeChanged || sectionNumberChanged;

        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionNumber = newSectionNumber.Value;

        // Update current if we have CurrentBiblePublicationSchedule
        if (stateValue.CurrentBiblePublicationSchedule != null)
        {
            current = mapper.Map<BiblePublicationSchedule>(stateValue.CurrentBiblePublicationSchedule);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal BiblePublicationSchedule from CurrentSchedule
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionNumber = newSectionNumber.Value,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
            };
            lastCurrent = current;
        }

        // If language, publication code, or section number changed, repopulate tracks
        if (needsRepopulation && initComplete)
        {
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                await initialize(newLanguageCode, newPublicationCode, newSectionNumber.Value);
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

    public void UpdateFromState(IState<ApplicationState> state, IMapper mapper)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        var newPublicationCode = currentSchedule.BiblePublicationPublicationCode;
        var newSectionNumber = currentSchedule.BiblePublicationSectionNumber;

        if (string.IsNullOrEmpty(newLanguageCode) || string.IsNullOrEmpty(newPublicationCode) || !newSectionNumber.HasValue)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;
        lastSectionNumber = newSectionNumber.Value;

        // Update current from CurrentSchedule
        if (stateValue.CurrentBiblePublicationSchedule != null)
        {
            current = mapper.Map<BiblePublicationSchedule>(stateValue.CurrentBiblePublicationSchedule);
        }
        else
        {
            current = new BiblePublicationSchedule
            {
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                SectionNumber = newSectionNumber.Value,
                TrackNumber = currentSchedule.BiblePublicationTrackNumber ?? 1
            };
        }
        lastCurrent = current;
    }

    public void SetInitComplete(bool value) => initComplete = value;
}

