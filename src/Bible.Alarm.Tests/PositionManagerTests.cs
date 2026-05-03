#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class PositionManagerTests
{
    [Fact]
    public void UpdatePositionFromMessage_sets_zero_when_position_missing()
    {
        string? time = null;
        double? progress = null;

        PositionManager.UpdatePositionFromMessage(
            new PlaybackPositionChangedMessage(),
            TimeSpan.FromMinutes(2),
            currentUiProgress: 0,
            t => time = t,
            p => progress = p);

        Assert.Equal("00:00", time);
        Assert.Equal(0.0, progress);
    }

    [Fact]
    public void UpdatePositionFromMessage_sets_progress_zero_when_duration_zero_but_formats_time()
    {
        string? time = null;
        double? progress = null;

        PositionManager.UpdatePositionFromMessage(
            new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(12) },
            currentDuration: TimeSpan.Zero,
            currentUiProgress: 0,
            t => time = t,
            p => progress = p);

        Assert.Equal("00:12", time);
        Assert.Equal(0.0, progress);
    }

    [Fact]
    public void UpdatePositionFromMessage_skips_progress_when_delta_below_threshold()
    {
        var progressCalls = 0;

        PositionManager.UpdatePositionFromMessage(
            new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(50) },
            TimeSpan.FromSeconds(100),
            currentUiProgress: 0.5,
            _ => { },
            _ => progressCalls++);

        Assert.Equal(0, progressCalls);
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_false_when_duration_not_positive()
    {
        Assert.False(PositionManager.ShouldIgnorePositionUpdate(0.5, 0.5, TimeSpan.Zero));
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_true_when_delta_small()
    {
        Assert.True(PositionManager.ShouldIgnorePositionUpdate(0.501, 0.5, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void ShouldIgnorePositionUpdate_false_when_delta_at_or_above_threshold()
    {
        Assert.False(PositionManager.ShouldIgnorePositionUpdate(0.52, 0.5, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void FormatTime_uses_hms_when_at_least_one_hour()
    {
        Assert.Equal("1:02:03", PositionManager.FormatTime(new TimeSpan(1, 2, 3)));
    }

    [Fact]
    public void FormatTime_exact_one_hour_uses_hms()
    {
        Assert.Equal("1:00:00", PositionManager.FormatTime(new TimeSpan(1, 0, 0)));
    }

    [Fact]
    public void FormatTime_uses_mm_ss_under_one_hour()
    {
        Assert.Equal("03:07", PositionManager.FormatTime(new TimeSpan(0, 3, 7)));
    }

    [Fact]
    public void CalculateProgress_zero_when_duration_not_positive()
    {
        Assert.Equal(0.0, PositionManager.CalculateProgress(TimeSpan.FromSeconds(30), TimeSpan.Zero));
    }

    [Fact]
    public void CalculateProgress_ratio_otherwise()
    {
        Assert.InRange(
            PositionManager.CalculateProgress(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60)),
            0.24,
            0.26);
    }

    [Fact]
    public void HandlePreparationProgressMessage_throttles_rapid_updates()
    {
        var sut = new PositionManager();
        var calls = 0;

        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 4,
            TotalBytesDownloaded = 10,
            TotalBytesExpected = null,
            CurrentTrackProgress = 0,
        };

        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.False(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_byte_complete_applies_even_when_throttled_otherwise()
    {
        var sut = new PositionManager();
        var calls = 0;
        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 4,
            TotalBytesDownloaded = 1000,
            TotalBytesExpected = 1000,
            CurrentTrackProgress = 0,
        };

        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_start_preparing_never_skipped_by_throttle()
    {
        var sut = new PositionManager();
        var calls = 0;
        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = 3,
            TotalBytesDownloaded = 0,
            CurrentTrackProgress = 0,
        };

        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_second_update_applies_after_throttle_window()
    {
        var sut = new PositionManager();
        var calls = 0;

        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 4,
            TotalBytesDownloaded = 10,
            TotalBytesExpected = null,
            CurrentTrackProgress = 0,
        };

        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Thread.Sleep(120);
        Assert.True(sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => calls++));
        Assert.Equal(2, calls);
    }


    [Fact]
    public void HandlePreparationProgressMessage_uses_byte_ratio_when_expected_known()
    {
        var sut = new PositionManager();
        double? lastProgress = null;
        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = 2,
            TotalBytesDownloaded = 250,
            TotalBytesExpected = 1000,
            CurrentTrackProgress = 0,
        };

        Assert.True(sut.HandlePreparationProgressMessage(
            msg,
            (_, _, p, _) => lastProgress = p));

        Assert.InRange(lastProgress!.Value, 0.24, 0.26);
    }

    [Fact]
    public void HandlePreparationProgressMessage_track_based_includes_current_track_contribution()
    {
        var sut = new PositionManager();
        double? lastProgress = null;
        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 2,
            TotalBytesDownloaded = 0,
            TotalBytesExpected = null,
            CurrentTrackProgress = 0.5,
        };

        Assert.True(sut.HandlePreparationProgressMessage(
            msg,
            (_, _, p, preparing) =>
            {
                lastProgress = p;
                Assert.True(preparing);
            }));

        Assert.InRange(lastProgress!.Value, 0.74, 0.76);
    }
}
