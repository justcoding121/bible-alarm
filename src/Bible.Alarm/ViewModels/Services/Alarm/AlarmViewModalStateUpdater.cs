#nullable enable
using Bible;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.ViewModels.Services.Alarm;

/// <summary>
/// Handles state updates for AlarmViewModal.
/// Separated from AlarmViewModal for better modularity.
/// </summary>
public class AlarmViewModalStateUpdater
{
    private readonly ILogger logger;
    private readonly Action<string> setTitle;
    private readonly Action<string> setSubTitle;
    private readonly Action<string> setDescription;
    private readonly Action<string> setEndTime;
    private readonly Action<string> setErrorMessage;
    private readonly Action<bool> setNextEnabled;
    private readonly Action<bool> setPreviousEnabled;
    private readonly Action<bool> setPlayVisible;
    private readonly Action<bool> setPauseVisible;
    private readonly Action<TimeSpan> setCurrentDuration;
    private readonly Action<string?> updateArtwork;
    private readonly Action notifyControlsEnabledChanged;
    private readonly Action notifyProgressTextChanged;
    private readonly Action notifyPreparationProgressChanged;
    private readonly Action notifyHasErrorChanged;

    private bool hasReceivedInitialState;
    private string? previousTrackTitle;
    private string? previousTrackArtist;
    private string? previousTrackAlbum;
    private string? previousArtworkUrl;
    private string? lastArtworkUrl;

    public AlarmViewModalStateUpdater(
        ILogger logger,
        Action<string> setTitle,
        Action<string> setSubTitle,
        Action<string> setDescription,
        Action<string> setEndTime,
        Action<string> setErrorMessage,
        Action<bool> setNextEnabled,
        Action<bool> setPreviousEnabled,
        Action<bool> setPlayVisible,
        Action<bool> setPauseVisible,
        Action<TimeSpan> setCurrentDuration,
        Action<string?> updateArtwork,
        Action notifyControlsEnabledChanged,
        Action notifyProgressTextChanged,
        Action notifyPreparationProgressChanged,
        Action notifyHasErrorChanged)
    {
        this.logger = logger;
        this.setTitle = setTitle;
        this.setSubTitle = setSubTitle;
        this.setDescription = setDescription;
        this.setEndTime = setEndTime;
        this.setErrorMessage = setErrorMessage;
        this.setNextEnabled = setNextEnabled;
        this.setPreviousEnabled = setPreviousEnabled;
        this.setPlayVisible = setPlayVisible;
        this.setPauseVisible = setPauseVisible;
        this.setCurrentDuration = setCurrentDuration;
        this.updateArtwork = updateArtwork;
        this.notifyControlsEnabledChanged = notifyControlsEnabledChanged;
        this.notifyProgressTextChanged = notifyProgressTextChanged;
        this.notifyPreparationProgressChanged = notifyPreparationProgressChanged;
        this.notifyHasErrorChanged = notifyHasErrorChanged;
    }

    public bool HasReceivedInitialState
    {
        get => hasReceivedInitialState;
        set => hasReceivedInitialState = value;
    }

    public bool DetectTrackChange(PlaybackState state)
    {
        var currentTitle = state.Title ?? "";
        var currentArtist = state.Artist ?? "";
        var currentAlbum = state.Album ?? "";

        return hasReceivedInitialState &&
               (previousTrackTitle != currentTitle ||
                previousTrackArtist != currentArtist ||
                previousTrackAlbum != currentAlbum);
    }

    public void HandleTrackChange(bool trackChanged, PlaybackState state)
    {
        if (trackChanged)
        {
            hasReceivedInitialState = false;
            notifyControlsEnabledChanged();
        }

        if (!hasReceivedInitialState &&
            (state.Status == PlayStatus.Playing || state.Status == PlayStatus.Paused))
        {
            hasReceivedInitialState = true;
            notifyControlsEnabledChanged();
        }

        if (hasReceivedInitialState)
        {
            previousTrackTitle = state.Title ?? "";
            previousTrackArtist = state.Artist ?? "";
            previousTrackAlbum = state.Album ?? "";
        }
    }

    public void UpdateControlsFromState(PlaybackState state)
    {
        if (state.IsPreparingOrPlaying && (state.Status == PlayStatus.Playing || state.Status == PlayStatus.Paused))
        {
            setNextEnabled(state.CanPlayNext);
            setPreviousEnabled(true);
        }
        else
        {
            setNextEnabled(false);
            setPreviousEnabled(false);
        }
    }

    public void UpdateMetadataFromState(PlaybackState state, bool trackChanged)
    {
        setTitle(state.Title ?? "");
        setSubTitle(state.Artist ?? "");
        setDescription(state.Album ?? "");

        var artworkUrl = state.ArtworkUrl;
        var shouldForceUpdate = trackChanged || (previousArtworkUrl != artworkUrl);
        if (shouldForceUpdate)
        {
            previousArtworkUrl = artworkUrl;
            if (trackChanged)
            {
                lastArtworkUrl = null;
            }
            updateArtwork(artworkUrl);
        }
    }

    public void UpdatePlaybackStateFromState(PlaybackState state)
    {
        var duration = state.Duration;
        setCurrentDuration(duration);
        setEndTime($"{duration.Minutes:00}:{duration.Seconds:00}");

        setErrorMessage(state.ErrorMessage ?? "");

        var isPlaying = state.Status == PlayStatus.Playing ||
                        (state.IsAutoAdvancing && state.Status != PlayStatus.Paused);
        setPlayVisible(!isPlaying);
        setPauseVisible(isPlaying);

        notifyProgressTextChanged();
        notifyPreparationProgressChanged();
        notifyHasErrorChanged();
        notifyControlsEnabledChanged();
    }
}

