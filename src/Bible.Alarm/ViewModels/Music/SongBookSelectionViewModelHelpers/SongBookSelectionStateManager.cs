#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.SongBookSelectionViewModelHelpers;

/// <summary>
/// Handles state management and initialization for SongBookSelectionViewModel.
/// </summary>
public sealed class SongBookSelectionStateManager(IMapper mapper)
{
    private AlarmMusic? current;
    private AlarmMusic? lastCurrent;
    private bool initComplete;

    // Track last music type and language code to detect changes
    private MusicType? lastMusicType;
    private string? lastLanguageCode;

    public AlarmMusic? Current => current;
    public bool InitComplete => initComplete;
    public MusicType? LastMusicType => lastMusicType;
    public string? LastLanguageCode => lastLanguageCode;

    public void InitializeCurrent(IState<ApplicationState> state)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
    }

    public void HandleMusicInitialized(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<Task> initialize)
    {
        if (initComplete)
        {
            MainThread.BeginInvokeOnMainThread(() => setBusy(false));
            return;
        }

        var stateValue = state.Value;

        // Use CurrentSchedule as the source of truth, not CurrentMusic
        if (stateValue.CurrentSchedule == null)
        {
            MainThread.BeginInvokeOnMainThread(() => setBusy(false));
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;

        // Require MusicType, but allow LanguageCode to be null/empty for Vocals
        // (user might be opening language modal to select a language)
        // For Melodies, LanguageCode can be null
        if (!newMusicType.HasValue)
        {
            MainThread.BeginInvokeOnMainThread(() => setBusy(false));
            return;
        }

        // For Vocals, we need LanguageCode eventually, but allow initialization without it
        // Initialize() will set a default language if needed
        // For Melodies, LanguageCode can be null

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;

        // Update current if we have CurrentMusic
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
            lastCurrent = current;
        }
        else
        {
            // Create a minimal AlarmMusic from CurrentSchedule
            // LanguageCode can be null/empty for Vocals when opening language modal
            current = new AlarmMusic
            {
                MusicType = newMusicType.Value,
                LanguageCode = newLanguageCode ?? string.Empty,
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
            await initialize();

            await Task.Delay(100);
            await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
        });
    }

    public void HandleMusicChanged(
        IState<ApplicationState> state,
        Action<bool> setBusy,
        Func<string, Task> populateSongBooks,
        Action setSelectedSongBook)
    {
        var stateValue = state.Value;

        if (stateValue.CurrentSchedule == null)
        {
            return;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        var newMusicType = currentSchedule.MusicType;
        var newLanguageCode = currentSchedule.MusicLanguageCode;

        if (!newMusicType.HasValue || string.IsNullOrEmpty(newLanguageCode))
        {
            return;
        }

        // Check if music type or language code changed
        var musicTypeChanged = lastMusicType != newMusicType.Value;
        var languageCodeChanged = lastLanguageCode != newLanguageCode;
        var needsRepopulation = musicTypeChanged || languageCodeChanged;

        if (!needsRepopulation && initComplete)
        {
            return;
        }

        // Update tracking variables
        lastMusicType = newMusicType.Value;
        lastLanguageCode = newLanguageCode;

        // Update current if we have CurrentMusic
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
                PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                Repeat = currentSchedule.MusicRepeat ?? false
            };
            lastCurrent = current;
        }

        // If music type or language changed, repopulate song books
        if (needsRepopulation && initComplete && newMusicType.Value == MusicType.Vocals)
        {
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(true));
                await populateSongBooks(newLanguageCode);
                await Task.Delay(100);
                await MainThread.InvokeOnMainThreadAsync(() => setBusy(false));
            });
        }
        else
        {
            // Update selected song book when state changes
            MainThread.BeginInvokeOnMainThread(setSelectedSongBook);
        }
    }

    public void EnsureCurrentIsSet(IState<ApplicationState> state, IMapper mapper)
    {
        if (current == null)
        {
            var currentItem = state.Value.CurrentMusic;
            if (currentItem != null)
            {
                current = mapper.Map<AlarmMusic>(currentItem);
            }
            else
            {
                // Create from CurrentSchedule if available
                var currentSchedule = state.Value.CurrentSchedule;
                if (currentSchedule != null && currentSchedule.MusicType.HasValue)
                {
                    current = new AlarmMusic
                    {
                        MusicType = currentSchedule.MusicType.Value,
                        LanguageCode = currentSchedule.MusicLanguageCode ?? string.Empty,
                        PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
                        TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
                        Repeat = currentSchedule.MusicRepeat ?? false
                    };
                }
            }
        }
    }

    public AlarmMusic? GetCurrentFromState(IState<ApplicationState> state, IMapper mapper)
    {
        var stateValue = state.Value;
        if (stateValue.CurrentSchedule == null || !stateValue.CurrentSchedule.MusicType.HasValue)
        {
            return null;
        }

        var currentSchedule = stateValue.CurrentSchedule;
        return new AlarmMusic
        {
            MusicType = currentSchedule.MusicType.Value,
            LanguageCode = currentSchedule.MusicLanguageCode ?? string.Empty,
            PublicationCode = currentSchedule.MusicPublicationCode ?? string.Empty,
            TrackNumber = currentSchedule.MusicTrackNumber ?? 1,
            Repeat = currentSchedule.MusicRepeat ?? false
        };
    }
}

