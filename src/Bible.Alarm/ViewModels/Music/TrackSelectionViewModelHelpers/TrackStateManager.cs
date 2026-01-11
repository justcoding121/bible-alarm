#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for TrackSelectionViewModel.
/// </summary>
public sealed class TrackStateManager(IMapper mapper)
{
    private AlarmMusic? current;
    private AlarmMusic? lastCurrent;
    private bool initComplete;

    // Track last music type, language code, and publication code to detect changes
    private MusicType? lastMusicType;
    private string? lastLanguageCode;
    private string? lastPublicationCode;

    public void InitializeCurrent(IState<ApplicationState> state)
    {
        var stateValue = state.Value;
        // Use CurrentSchedule as the source of truth, with CurrentMusic as fallback
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
        else if (stateValue.CurrentSchedule != null && stateValue.CurrentSchedule.MusicType.HasValue)
        {
            // Create a minimal AlarmMusic from CurrentSchedule
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
        {
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentMusic
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // For melodies: only require MusicType and PublicationCode (LanguageCode can be null)
        // For vocals: require MusicType, LanguageCode, and PublicationCode
        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // For vocals, language code is required
        if (newMusicType.Value == MusicType.Vocals && string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentMusic (for other properties like TrackNumber)
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal AlarmMusic from CurrentSchedule
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

                // For Melodies: only need PublicationCode (already checked above)
                // For Vocals: need LanguageCode and PublicationCode (already checked above)
                // So if we got here, we have all required properties
                if (current != null && !string.IsNullOrEmpty(current.PublicationCode))
                {
                    await initializeTracks();
                }

                // CollectionView needs a moment to render before setting selected track and hiding busy
                await Task.Delay(100);

                // Set selected track after tracks are populated
                await MainThread.InvokeOnMainThreadAsync(setSelectedTrack);

                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
                Log.Error(ex, "Error initializing TrackStateManager");
            }
        });
    }

    public void HandleMusicChanged(IState<ApplicationState> state, Action<bool> setBusy, Func<string?, string, Task> initializeTracks, Action setSelectedTrack)
    {
        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentMusic
        // CurrentSchedule is updated first and is authoritative
        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;
        var newPublicationCode = currentSchedule.MusicPublicationCode;

        // For melodies: only require MusicType and PublicationCode (LanguageCode can be null)
        // For vocals: require MusicType, LanguageCode, and PublicationCode
        if (!newMusicType.HasValue || string.IsNullOrEmpty(newPublicationCode))
        {
            return;
        }

        // For vocals, language code is required
        if (newMusicType.Value == MusicType.Vocals && string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Check if music type, language code, or publication code changed (need to repopulate tracks)
        var musicTypeChanged = lastMusicType != newMusicType.Value;
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var publicationCodeChanged = lastPublicationCode != newPublicationCode;
        var needsRepopulation = musicTypeChanged || languageCodeChanged || publicationCodeChanged;

        // If no changes detected and we're already initialized, skip
        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;
        lastPublicationCode = newPublicationCode;

        // Update current if we have CurrentMusic (for other properties like TrackNumber)
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal AlarmMusic from CurrentSchedule
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

        // If music type, language, or publication changed, repopulate tracks
        if (needsRepopulation && initComplete)
        {
            // For melodies, LanguageCode can be null, so only check PublicationCode
            // For vocals, LanguageCode is required (already checked above)
            if (newPublicationCode == null)
            {
                return;
            }
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                // Pass null/empty for languageCode if it's a melody (LanguageCode can be null for melodies)
                await initializeTracks(newLanguageCode ?? string.Empty, newPublicationCode);
                await Task.Delay(100); // Give CollectionView time to render
                // Set selected track after tracks are populated
                await MainThread.InvokeOnMainThreadAsync(setSelectedTrack);
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
            });
        }
        else
        {
            // Update selected track when state changes (e.g., after navigating back)
            MainThread.BeginInvokeOnMainThread(setSelectedTrack);
        }
    }

    public AlarmMusic? Current => current;
    public bool InitComplete => initComplete;
    public MusicType? LastMusicType => lastMusicType;
    public string? LastLanguageCode => lastLanguageCode;
    public string? LastPublicationCode => lastPublicationCode;
}
