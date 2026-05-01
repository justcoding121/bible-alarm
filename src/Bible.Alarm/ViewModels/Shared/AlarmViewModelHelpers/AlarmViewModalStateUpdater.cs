#nullable enable
using System;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles state updates for AlarmViewModal.
/// Separated from AlarmViewModal for better modularity.
/// </summary>
public class AlarmViewModalStateUpdater
{
    /// <summary>Constructor dependencies for <see cref="AlarmViewModalStateUpdater"/> (avoids excessive parameter lists).</summary>
    public sealed class Options
    {
        public required Action<string> SetTitle { get; init; }
        public required Action<string> SetSubTitle { get; init; }
        public required Action<string> SetDescription { get; init; }
        public required Action<string> SetEndTime { get; init; }
        public required Action<string> SetErrorMessage { get; init; }
        public required Action<bool> SetNextEnabled { get; init; }
        public required Action<bool> SetPreviousEnabled { get; init; }
        public required Action<bool> SetPlayVisible { get; init; }
        public required Action<bool> SetPauseVisible { get; init; }
        public required Action<TimeSpan> SetCurrentDuration { get; init; }
        public required Action<string?, string?, bool> UpdateArtwork { get; init; }
        public required Action<bool> SetWaitingForArtwork { get; init; }
        public required Action NotifyControlsEnabledChanged { get; init; }
        public required Action NotifyProgressTextChanged { get; init; }
        public required Action NotifyPreparationProgressChanged { get; init; }
        public required Action NotifyHasErrorChanged { get; init; }
    }

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
    private readonly Action<bool> setWaitingForArtwork;
    private readonly Action notifyControlsEnabledChanged;
    private readonly Action notifyProgressTextChanged;
    private readonly Action notifyPreparationProgressChanged;
    private readonly Action notifyHasErrorChanged;

    private string? previousTrackTitle;
    private string? previousTrackArtist;
    private string? previousTrackAlbum;
    private string? previousArtworkUrl;
    private bool previousIsTransitioningTrack;
    private PlayStatus previousStatus;
    private bool hasReachedPlayingForCurrentTrack;

    public AlarmViewModalStateUpdater(Options options)
    {
        setTitle = options.SetTitle;
        setSubTitle = options.SetSubTitle;
        setDescription = options.SetDescription;
        setEndTime = options.SetEndTime;
        setErrorMessage = options.SetErrorMessage;
        setNextEnabled = options.SetNextEnabled;
        setPreviousEnabled = options.SetPreviousEnabled;
        setPlayVisible = options.SetPlayVisible;
        setPauseVisible = options.SetPauseVisible;
        setCurrentDuration = options.SetCurrentDuration;
        updateArtwork = options.UpdateArtwork;
        setWaitingForArtwork = options.SetWaitingForArtwork;
        notifyControlsEnabledChanged = options.NotifyControlsEnabledChanged;
        notifyProgressTextChanged = options.NotifyProgressTextChanged;
        notifyPreparationProgressChanged = options.NotifyPreparationProgressChanged;
        notifyHasErrorChanged = options.NotifyHasErrorChanged;
    }

    public bool HasReceivedInitialState { get; set; }

    public bool DetectTrackChange(PlaybackState state)
    {
        var currentTitle = state.Title ?? "";
        var currentArtist = state.Artist ?? "";
        var currentAlbum = state.Album ?? "";

        return HasReceivedInitialState &&
               (previousTrackTitle != currentTitle ||
                previousTrackArtist != currentArtist ||
                previousTrackAlbum != currentAlbum);
    }

    public void HandleTrackChange(bool trackChanged, PlaybackState state)
    {
        if (trackChanged)
        {
            HasReceivedInitialState = false;
            hasReachedPlayingForCurrentTrack = false;
            notifyControlsEnabledChanged();
        }

        if (!HasReceivedInitialState &&
            (state.Status == PlayStatus.Playing || state.Status == PlayStatus.Paused))
        {
            HasReceivedInitialState = true;
            notifyControlsEnabledChanged();
        }

        if (HasReceivedInitialState)
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
            setNextEnabled(true);
            setPreviousEnabled(true);
        }
        else if (state.IsPreparingOrPlaying && state.Status == PlayStatus.Stopped)
        {
            // Transient Stopped during track transitions (auto-advance). MediaElement fires
            // Paused→Stopped before OnMediaEnded dispatches IsAutoAdvancing/IsTransitioningTrack.
            // Keep prev/next as-is to avoid a partial-disable flash; AreControlsEnabled handles
            // disabling all controls uniformly.
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
        string? artworkUrl;
        if (inPlayback)
        {
            artworkUrl = state.ArtworkUrl;
        }
        else
        {
            artworkUrl = state.ArtworkUrl ?? state.DefaultScheduleArtworkUrl;
        }
        string? fallbackUrl = null;
        if (!inPlayback && state.ArtworkUrl != null)
        {
            fallbackUrl = state.DefaultScheduleArtworkUrl;
        }

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

        var hasTrackMetadata = inPlayback && (!string.IsNullOrEmpty(state.Title) || !string.IsNullOrEmpty(state.Artist));
        var waitingForArtwork = hasTrackMetadata && string.IsNullOrEmpty(artworkUrl) && !state.IsTransitioningTrack;
        setWaitingForArtwork(waitingForArtwork);

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
                        state.IsAutoAdvancing ||
                        state.IsTransitioningTrack ||
                        (state.IsPreparingOrPlaying && state.Status == PlayStatus.Stopped);
        setPlayVisible(!isPlaying);
        setPauseVisible(isPlaying);

        notifyProgressTextChanged();
        notifyPreparationProgressChanged();
        notifyHasErrorChanged();
        notifyControlsEnabledChanged();
    }
}

