#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

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
    private readonly Action<string?, string?, bool> updateArtwork;
    private readonly Action notifyControlsEnabledChanged;
    private readonly Action notifyProgressTextChanged;
    private readonly Action notifyPreparationProgressChanged;
    private readonly Action notifyHasErrorChanged;

    private bool hasReceivedInitialState;
    private string? previousTrackTitle;
    private string? previousTrackArtist;
    private string? previousTrackAlbum;
    private string? previousArtworkUrl;
    private bool previousIsTransitioningTrack;
    private PlayStatus previousStatus;
    private bool hasReachedPlayingForCurrentTrack;

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
        Action<string?, string?, bool> updateArtwork,
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
            hasReachedPlayingForCurrentTrack = false;
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
            // Next/Previous are always enabled during playback.
            setNextEnabled(true);
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

        // During active playback for a schedule, use only track artwork so we don't flash
        // default schedule icon then track artwork (or flip between them). Use default schedule
        // artwork only when not playing a specific schedule (e.g. idle/car screen).
        var inPlayback = state.CurrentScheduleId.HasValue;
        var artworkUrl = inPlayback
            ? state.ArtworkUrl
            : (state.ArtworkUrl ?? state.DefaultScheduleArtworkUrl);
        var fallbackUrl = inPlayback
            ? null
            : (state.ArtworkUrl != null ? state.DefaultScheduleArtworkUrl : null);

        // Re-apply artwork when: track changed, URL changed, transition just ended, or status first became Playing.
        // - Transition ended: async artwork may have arrived during IsTransitioningTrack when the UI missed it.
        // - Status -> Playing (first time only): async artwork may have arrived during Loading; re-apply when playback starts.
        //   Excludes Loading->Playing from seek buffering; hasReachedPlayingForCurrentTrack resets on track change.
        var transitionJustEnded = previousIsTransitioningTrack && !state.IsTransitioningTrack;
        var statusFirstBecamePlaying = !hasReachedPlayingForCurrentTrack &&
            previousStatus != PlayStatus.Playing &&
            state.Status == PlayStatus.Playing;
        if (state.Status == PlayStatus.Playing)
        {
            hasReachedPlayingForCurrentTrack = true;
        }

        var shouldForceUpdate = trackChanged ||
            (previousArtworkUrl != artworkUrl) ||
            transitionJustEnded ||
            (statusFirstBecamePlaying && !string.IsNullOrEmpty(artworkUrl));
        if (shouldForceUpdate)
        {
            previousArtworkUrl = artworkUrl;
            updateArtwork(artworkUrl, fallbackUrl, trackChanged || transitionJustEnded || statusFirstBecamePlaying);
        }

        previousIsTransitioningTrack = state.IsTransitioningTrack;
        previousStatus = state.Status;
    }

    public void UpdatePlaybackStateFromState(PlaybackState state)
    {
        var duration = state.Duration;
        setCurrentDuration(duration);
        setEndTime(PositionManager.FormatTime(duration));

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

