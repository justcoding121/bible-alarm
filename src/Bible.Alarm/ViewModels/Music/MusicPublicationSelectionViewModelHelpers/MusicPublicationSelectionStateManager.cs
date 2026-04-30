#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Fluxor;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for MusicPublicationSelectionViewModel.
/// Music type (melody vs. vocal) is inferred from LanguageCode: NULL = melody, non-NULL = vocal.
/// </summary>
public sealed class MusicPublicationSelectionStateManager
{
    private AlarmMusic? current;
    private AlarmMusic? lastCurrent;
    private bool initComplete;

    // Track last language code to detect changes
    private string? lastLanguageCode;

    public AlarmMusic? Current => current;
    public bool InitComplete => initComplete;
    public string? LastLanguageCode => lastLanguageCode;

    public void InitializeCurrent(IState<ApplicationState> state)
    {
        var stateValue = state.Value;
        // Derive from CurrentSchedule (single source of truth)
        if (stateValue.CurrentSchedule != null && !string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
        {
            var schedule = stateValue.CurrentSchedule;
            current = new AlarmMusic
            {
                LanguageCode = schedule.MusicLanguageCode,
                PublicationCode = schedule.MusicPublicationCode ?? string.Empty,
                TrackCode = schedule.MusicTrackCode ?? string.Empty,
                Repeat = schedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }
    }

    public void HandleMusicInitialized(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<Task> initialize)
    {
        // Note: Do NOT set IsBusy = false on early returns - the modal controls this via ModalScrollHelper
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.MusicLanguageCode;

        // Allow initialization with or without language code
        // For melody music, language code is null; for vocal music, it's required

        // Update tracking variables
        lastLanguageCode = newLanguageCode;

        // Update current from CurrentSchedule (single source of truth — non-null after guard above)
        current = new AlarmMusic
        {
            LanguageCode = newLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
            TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
        lastCurrent = current;

        initComplete = true;
        Task.Run(async () =>
        {
            // Note: Do NOT set setBusy(true) here - the modal controls the busy state via ModalScrollHelper
            // Setting it here would interfere with the modal's scroll-then-hide-overlay flow
            await initialize();
            // Note: Do NOT set setBusy(false) here - the modal controls this via ModalScrollHelper
            // The modal will set IsBusy = false after scrolling to the selected item completes
        });
    }

    public void HandleMusicChanged(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<string?, Task> populateSongPublications,
        Action setSelectedSongPublication)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newLanguageCode = currentSchedule.MusicLanguageCode;

        // Check if language code changed
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var needsRepopulation = languageCodeChanged;

        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastLanguageCode = newLanguageCode;

        // Non-null CurrentSchedule ensured above
        current = new AlarmMusic
        {
            LanguageCode = newLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
            TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
        lastCurrent = current;

        // If language changed, repopulate song sections
        if (needsRepopulation && initComplete)
        {
            Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                    await populateSongPublications(newLanguageCode);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, AppConstants.Logging.MusicPublicationSelectionStateManagerDiagnosticsLog.ErrorInHandleMusicChangedDuringPublicationPopulation);
                }
                finally
                {
                    await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
                }
            });
        }
        else
        {
            // Update selected song section when state changes
            MainThread.BeginInvokeOnMainThread(setSelectedSongPublication);
        }
    }

    public void EnsureCurrentIsSet(IState<ApplicationState> state, IMapper mapper)
    {
        if (current == null)
        {
            // Derive from CurrentSchedule (single source of truth)
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null && !string.IsNullOrEmpty(currentSchedule.MusicPublicationCode))
            {
                current = new AlarmMusic
                {
                    LanguageCode = currentSchedule.MusicLanguageCode,
                    PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                    TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
                    Repeat = currentSchedule.MusicRepeat ?? false
                };
            }
        }
    }

    public static AlarmMusic? GetCurrentFromState(IState<ApplicationState> state)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null || string.IsNullOrEmpty(stateValue.CurrentSchedule.MusicPublicationCode))
        {
            return null;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        return new AlarmMusic
        {
            LanguageCode = currentSchedule.MusicLanguageCode,
            PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
            TrackCode = currentSchedule.MusicTrackCode ?? string.Empty,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
    }
}
