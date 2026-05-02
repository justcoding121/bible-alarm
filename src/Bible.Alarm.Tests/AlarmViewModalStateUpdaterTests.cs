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
}
