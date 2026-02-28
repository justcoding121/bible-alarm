#nullable enable
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for MusicTrackSelectionViewModel.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental, otherwise = vocal.
/// </summary>
public sealed class MusicTrackStateManager
{
    private AlarmMusic? current;
    private AlarmMusic? lastCurrent;
    private bool initComplete;
    private string? lastLanguageCode;
    private string? lastPublicationCode;
    private string? lastLoadedSectionCode;

    public void InitializeCurrent(IState<ApplicationState> state)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
        {
            var currentSchedule = stateValue.CurrentSchedule;
            current = new AlarmMusic
            {
                LanguageCode = currentSchedule.MusicLanguageCode,
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                SectionCode = currentSchedule.MusicSectionCode,
                TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }
    }

    public void SetLastLoadedSection(string? sectionCode)
    {
        lastLoadedSectionCode = sectionCode;
    }

    public void HandleMusicInitialized(IState<ApplicationState> state, Action<bool> setBusy, Func<Task> initializeTracks, Action setSelectedTrack)
    {
        if (initComplete)
            return;

        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
            return;

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // Must have a publication code to proceed
        if (string.IsNullOrEmpty(newPublicationCode))
            return;

        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        current = new AlarmMusic
        {
            LanguageCode = newLanguageCode,
            PublicationCode = newPublicationCode,
            SectionCode = currentSchedule.MusicSectionCode,
            TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
        lastCurrent = current;

        initComplete = true;

        Task.Run(async () =>
        {
            try
            {
                // Note: Do NOT set setBusy(true) here - the modal controls the busy state via ModalScrollHelper
                if (current != null && !string.IsNullOrEmpty(current.PublicationCode))
                    await initializeTracks();
                await MainThread.InvokeOnMainThreadAsync(setSelectedTrack);
                // Note: Do NOT set IsBusy = false here - the modal controls this via ModalScrollHelper
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing MusicTrackStateManager");
                // Note: Do NOT set IsBusy = false here - the modal controls this
            }
        });
    }

    public void HandleMusicChanged(IState<ApplicationState> state, Action<bool> setBusy, Func<string?, string, Task> initializeTracks, Action setSelectedTrack)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
            return;

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // Must have a publication code to proceed
        if (string.IsNullOrEmpty(newPublicationCode))
            return;

        var newSectionCode = currentSchedule.MusicSectionCode;
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var sectionCodeChanged = lastLoadedSectionCode != newSectionCode;
        var needsRepopulation = languageCodeChanged || publicationCodeChanged || sectionCodeChanged;

        if (!needsRepopulation && initComplete)
            return;

        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        current = new AlarmMusic
        {
            LanguageCode = newLanguageCode,
            PublicationCode = newPublicationCode,
            SectionCode = newSectionCode,
            TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
        lastCurrent = current;

        if (needsRepopulation && initComplete && newPublicationCode != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                    await initializeTracks(newLanguageCode, newPublicationCode);
                    await MainThread.InvokeOnMainThreadAsync(setSelectedTrack);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error in MusicTrackStateManager.HandleMusicChanged during track population");
                }
                finally
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
                }
            });
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(setSelectedTrack);
        }
    }

    public AlarmMusic? Current => current;
    public bool InitComplete => initComplete;
    public string? LastLanguageCode => lastLanguageCode;
    public string? LastPublicationCode => lastPublicationCode;
    public string? LastLoadedSectionCode => lastLoadedSectionCode;
}
