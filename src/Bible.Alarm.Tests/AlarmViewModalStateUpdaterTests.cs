#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class AlarmViewModalStateUpdaterTests
{
    private sealed class CaptureOptions
    {
        public bool NextEnabled { get; set; }
        public bool PreviousEnabled { get; set; }
        public bool PlayVisible { get; set; }
        public bool PauseVisible { get; set; }
        public TimeSpan CurrentDuration { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool WaitingForArtwork { get; set; }
        public List<(string? Url, string? Fallback, bool Force)> ArtworkCalls { get; } = [];
        public int ControlsNotifyCount { get; set; }
        public int ProgressTextNotifyCount { get; set; }
        public int PreparationNotifyCount { get; set; }
        public int HasErrorNotifyCount { get; set; }

        public AlarmViewModalStateUpdater.Options BuildOptions() =>
            new()
            {
                SetTitle = v => Title = v,
                SetSubTitle = v => SubTitle = v,
                SetDescription = v => Description = v,
                SetEndTime = _ => { },
                SetErrorMessage = v => ErrorMessage = v,
                SetNextEnabled = v => NextEnabled = v,
                SetPreviousEnabled = v => PreviousEnabled = v,
                SetPlayVisible = v => PlayVisible = v,
                SetPauseVisible = v => PauseVisible = v,
                SetCurrentDuration = v => CurrentDuration = v,
                UpdateArtwork = (u, f, force) => ArtworkCalls.Add((u, f, force)),
                SetWaitingForArtwork = v => WaitingForArtwork = v,
                NotifyControlsEnabledChanged = () => ControlsNotifyCount++,
                NotifyProgressTextChanged = () => ProgressTextNotifyCount++,
                NotifyPreparationProgressChanged = () => PreparationNotifyCount++,
                NotifyHasErrorChanged = () => HasErrorNotifyCount++,
            };
    }

    private static PlaybackState Playing(string title = "T", string artist = "A", string album = "Al") =>
        new()
        {
            Status = PlayStatus.Playing,
            Title = title,
            Artist = artist,
            Album = album,
            Duration = TimeSpan.FromMinutes(3),
        };

    [Fact]
    public void DetectTrackChange_false_until_initial_state_received()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());
        sut.HasReceivedInitialState = false;

        Assert.False(sut.DetectTrackChange(Playing()));
    }

    [Fact]
    public void DetectTrackChange_true_when_metadata_changes_after_initial_state()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());
        sut.HandleTrackChange(trackChanged: false, Playing("First", "a", "b"));

        Assert.True(sut.DetectTrackChange(Playing("Second", "a", "b")));
    }

    [Fact]
    public void UpdateControlsFromState_enables_nav_when_preparing_or_playing_and_play_or_paused()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateControlsFromState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            Status = PlayStatus.Playing,
        });

        Assert.True(cap.NextEnabled);
        Assert.True(cap.PreviousEnabled);

        sut.UpdateControlsFromState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            Status = PlayStatus.Paused,
        });

        Assert.True(cap.NextEnabled);
        Assert.True(cap.PreviousEnabled);
    }

    [Fact]
    public void UpdateControlsFromState_disables_nav_when_not_active_playback()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateControlsFromState(new PlaybackState
        {
            IsPreparingOrPlaying = false,
            Status = PlayStatus.Stopped,
        });

        Assert.False(cap.NextEnabled);
        Assert.False(cap.PreviousEnabled);
    }

    [Fact]
    public void UpdateMetadataFromState_sets_text_and_artwork_when_schedule_playback()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(new PlaybackState
        {
            CurrentScheduleId = 9,
            Title = "Track",
            Artist = "Artist",
            Album = "Album",
            ArtworkUrl = "https://art",
            Status = PlayStatus.Playing,
            IsTransitioningTrack = false,
        }, trackChanged: true);

        Assert.Equal("Track", cap.Title);
        Assert.Equal("Artist", cap.SubTitle);
        Assert.Equal("Album", cap.Description);
        Assert.Single(cap.ArtworkCalls);
        Assert.Equal("https://art", cap.ArtworkCalls[0].Url);
    }

    [Fact]
    public void UpdatePlaybackStateFromState_sets_play_pause_and_duration()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdatePlaybackStateFromState(new PlaybackState
        {
            Status = PlayStatus.Playing,
            Duration = TimeSpan.FromMinutes(5),
            ErrorMessage = "oops",
            IsAutoAdvancing = false,
            IsTransitioningTrack = false,
            IsPreparingOrPlaying = false,
        });

        Assert.Equal(TimeSpan.FromMinutes(5), cap.CurrentDuration);
        Assert.False(cap.PlayVisible);
        Assert.True(cap.PauseVisible);
        Assert.Equal("oops", cap.ErrorMessage);
        Assert.True(cap.ProgressTextNotifyCount > 0);
    }

    [Fact]
    public void UpdateControlsFromState_preparing_but_stopped_leaves_nav_enabled_when_already_enabled()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateControlsFromState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            Status = PlayStatus.Playing,
        });

        Assert.True(cap.NextEnabled);

        sut.UpdateControlsFromState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            Status = PlayStatus.Stopped,
        });

        Assert.True(cap.NextEnabled);
    }

    [Fact]
    public void UpdateMetadataFromState_idle_merges_default_schedule_artwork()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(new PlaybackState
        {
            CurrentScheduleId = null,
            Title = "Idle",
            Artist = "A",
            Album = "B",
            ArtworkUrl = null,
            DefaultScheduleArtworkUrl = "https://wallpaper",
            Status = PlayStatus.Paused,
            IsTransitioningTrack = false,
        }, trackChanged: true);

        Assert.Single(cap.ArtworkCalls);
        Assert.Equal("https://wallpaper", cap.ArtworkCalls[0].Url);
        Assert.Null(cap.ArtworkCalls[0].Fallback);
    }

    [Fact]
    public void UpdateMetadataFromState_idle_when_artwork_present_passes_fallback_default_url()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(new PlaybackState
        {
            CurrentScheduleId = null,
            ArtworkUrl = "https://idle-thumb",
            DefaultScheduleArtworkUrl = "https://schedule-default",
            Title = "T",
            Status = PlayStatus.Paused,
            IsTransitioningTrack = false,
        }, trackChanged: true);

        Assert.Single(cap.ArtworkCalls);
        Assert.Equal("https://idle-thumb", cap.ArtworkCalls[0].Url);
        Assert.Equal("https://schedule-default", cap.ArtworkCalls[0].Fallback);
    }

    [Fact]
    public void UpdateMetadataFromState_transition_end_refires_artwork_even_when_url_unchanged()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(new PlaybackState
        {
            CurrentScheduleId = 1,
            Title = "T",
            ArtworkUrl = "https://art",
            Status = PlayStatus.Paused,
            IsTransitioningTrack = true,
        }, trackChanged: false);

        sut.UpdateMetadataFromState(new PlaybackState
        {
            CurrentScheduleId = 1,
            Title = "T",
            ArtworkUrl = "https://art",
            Status = PlayStatus.Playing,
            IsTransitioningTrack = false,
        }, trackChanged: false);

        Assert.Equal(2, cap.ArtworkCalls.Count);
    }

    [Fact]
    public void UpdateMetadataFromState_sets_waiting_when_schedule_has_text_but_no_artwork()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(
            new PlaybackState
            {
                CurrentScheduleId = 1,
                Title = "Chapter",
                Artist = "",
                Album = "",
                ArtworkUrl = null,
                Status = PlayStatus.Playing,
                IsTransitioningTrack = false,
            },
            trackChanged: false);

        Assert.True(cap.WaitingForArtwork);
    }

    [Fact]
    public void UpdateMetadataFromState_refires_artwork_when_status_first_becomes_playing()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(
            new PlaybackState
            {
                CurrentScheduleId = 1,
                Title = "T",
                ArtworkUrl = "https://cover",
                Status = PlayStatus.Loading,
                IsTransitioningTrack = false,
            },
            trackChanged: false);

        var afterLoading = cap.ArtworkCalls.Count;

        sut.UpdateMetadataFromState(
            new PlaybackState
            {
                CurrentScheduleId = 1,
                Title = "T",
                ArtworkUrl = "https://cover",
                Status = PlayStatus.Playing,
                IsTransitioningTrack = false,
            },
            trackChanged: false);

        Assert.True(cap.ArtworkCalls.Count > afterLoading);
    }

    [Fact]
    public void UpdatePlaybackStateFromState_pause_visible_when_auto_advancing()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdatePlaybackStateFromState(
            new PlaybackState
            {
                Status = PlayStatus.Stopped,
                IsAutoAdvancing = true,
                Duration = TimeSpan.Zero,
            });

        Assert.False(cap.PlayVisible);
        Assert.True(cap.PauseVisible);
    }

    [Fact]
    public void UpdatePlaybackStateFromState_pause_visible_when_transitioning_track()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdatePlaybackStateFromState(
            new PlaybackState
            {
                Status = PlayStatus.Paused,
                IsTransitioningTrack = true,
                Duration = TimeSpan.Zero,
            });

        Assert.False(cap.PlayVisible);
        Assert.True(cap.PauseVisible);
    }

    [Fact]
    public void UpdatePlaybackStateFromState_pause_visible_when_transient_stopped_during_session()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdatePlaybackStateFromState(
            new PlaybackState
            {
                Status = PlayStatus.Stopped,
                IsPreparingOrPlaying = true,
                Duration = TimeSpan.Zero,
            });

        Assert.False(cap.PlayVisible);
        Assert.True(cap.PauseVisible);
    }

    [Fact]
    public void HandleTrackChange_when_track_changed_resets_initial_state_until_playing_or_paused()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.HandleTrackChange(trackChanged: false, Playing());
        Assert.True(sut.HasReceivedInitialState);

        var notifiesBefore = cap.ControlsNotifyCount;
        sut.HandleTrackChange(
            trackChanged: true,
            new PlaybackState
            {
                Status = PlayStatus.Loading,
                Title = "Next",
                Artist = "a",
                Album = "b",
            });

        Assert.False(sut.HasReceivedInitialState);
        Assert.Equal(notifiesBefore + 1, cap.ControlsNotifyCount);
    }

    [Fact]
    public void HandleTrackChange_paused_establishes_initial_state()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.HandleTrackChange(
            trackChanged: false,
            new PlaybackState
            {
                Status = PlayStatus.Paused,
                Title = "T",
            });

        Assert.True(sut.HasReceivedInitialState);
        Assert.True(cap.ControlsNotifyCount > 0);
    }

    [Fact]
    public void UpdateMetadataFromState_not_waiting_while_transitioning_without_artwork()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        sut.UpdateMetadataFromState(
            new PlaybackState
            {
                CurrentScheduleId = 1,
                Title = "T",
                ArtworkUrl = null,
                Status = PlayStatus.Playing,
                IsTransitioningTrack = true,
            },
            trackChanged: false);

        Assert.False(cap.WaitingForArtwork);
    }

    [Fact]
    public void UpdateMetadataFromState_repeated_stable_playing_state_skips_redundant_artwork()
    {
        var cap = new CaptureOptions();
        var sut = new AlarmViewModalStateUpdater(cap.BuildOptions());

        var stable = new PlaybackState
        {
            CurrentScheduleId = 1,
            Title = "T",
            ArtworkUrl = "https://art",
            Status = PlayStatus.Playing,
            IsTransitioningTrack = false,
        };

        sut.UpdateMetadataFromState(stable, trackChanged: true);
        sut.UpdateMetadataFromState(stable, trackChanged: false);

        Assert.Single(cap.ArtworkCalls);
    }
}
