#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for MusicTrackSelectionViewModel.
/// </summary>
public sealed class MusicTrackStateManager
{
    private AlarmMusic? current;
    private AlarmMusic? lastCurrent;
    private bool initComplete;
    private MusicType? lastMusicType;
    private string? lastLanguageCode;
    private string? lastPublicationCode;

    public void InitializeCurrent(IState<ApplicationState> state)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule != null && stateValue.CurrentSchedule.MusicType.HasValue)
        {
            var currentSchedule = stateValue.CurrentSchedule;
            current = new AlarmMusic
            {
                MusicType = currentSchedule.MusicType.Value,
                LanguageCode = currentSchedule.MusicLanguageCode ?? string.Empty,
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }
    }

    public void HandleMusicInitialized(IState<ApplicationState> state, Action<bool> setBusy, Func<Task> initializeTracks, Action setSelectedTrack)
    {
        if (initComplete)
            return;

        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
            return;

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
            return;
        if (newMusicType.Value == MusicType.VocalMusic && string.IsNullOrEmpty(newLanguageCode))
            return;

        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        if (currentSchedule != null)
        {
            current = new AlarmMusic
            {
                MusicType = newMusicType.Value,
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        initComplete = true;

        Task.Run(async () =>
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                if (current != null && !string.IsNullOrEmpty(current.PublicationCode))
                    await initializeTracks();
                await Task.Delay(100);
                await MainThread.InvokeOnMainThreadAsync(setSelectedTrack);
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
                Log.Error(ex, "Error initializing MusicTrackStateManager");
            }
        });
    }

    public void HandleMusicChanged(IState<ApplicationState> state, Action<bool> setBusy, Func<string?, string, Task> initializeTracks, Action setSelectedTrack)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null)
            return;

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
            return;
        if (newMusicType.Value == MusicType.VocalMusic && string.IsNullOrEmpty(newLanguageCode))
            return;

        var musicTypeChanged = lastMusicType != newMusicType.Value;
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = musicTypeChanged || languageCodeChanged || publicationCodeChanged;

        if (!needsRepopulation && initComplete)
            return;

        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        if (currentSchedule != null)
        {
            current = new AlarmMusic
            {
                MusicType = newMusicType.Value,
                LanguageCode = newLanguageCode,
                PublicationCode = newPublicationCode,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        if (needsRepopulation && initComplete && newPublicationCode != null)
        {
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                await initializeTracks(newLanguageCode ?? string.Empty, newPublicationCode);
                await Task.Delay(100);
                await MainThread.InvokeOnMainThreadAsync(setSelectedTrack);
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
            });
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(setSelectedTrack);
        }
    }

    public AlarmMusic? Current => current;
    public bool InitComplete => initComplete;
    public MusicType? LastMusicType => lastMusicType;
    public string? LastLanguageCode => lastLanguageCode;
    public string? LastPublicationCode => lastPublicationCode;
}
