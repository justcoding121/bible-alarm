#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MessageHandlerTests
{
    [Fact]
    public void HandlePlaybackPositionMessage_returns_early_when_ignore_predicate_matches()
    {
        var progressTextCalls = 0;

        MessageHandler.HandlePlaybackPositionMessage(
            new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(30) },
            TimeSpan.FromSeconds(100),
            currentUiProgress: 0,
            _ => true,
            _ => { },
            _ => { },
            () => progressTextCalls++);

        Assert.Equal(0, progressTextCalls);
    }

    [Fact]
    public void HandlePlaybackPositionMessage_invokes_ui_updates_and_progress_text()
    {
        string? timeText = null;
        double? progress = null;
        var progressTextCalls = 0;

        MessageHandler.HandlePlaybackPositionMessage(
            new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(90) },
            TimeSpan.FromSeconds(180),
            currentUiProgress: 0,
            _ => false,
            t => timeText = t,
            p => progress = p,
            () => progressTextCalls++);

        Assert.NotNull(timeText);
        Assert.NotNull(progress);
        Assert.InRange(progress!.Value, 0.49, 0.51);
        Assert.Equal(1, progressTextCalls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_updates_state_and_progress_text_on_success()
    {
        var manager = new PositionManager();
        var sut = new MessageHandler(manager);
        var stateCalls = 0;
        var textCalls = 0;

        sut.HandlePreparationProgressMessage(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 3,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
            },
            (_, _, _, _) => stateCalls++,
            () => textCalls++);

        Assert.Equal(1, stateCalls);
        Assert.Equal(1, textCalls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_byte_complete_not_throttled_through_handler()
    {
        var manager = new PositionManager();
        var sut = new MessageHandler(manager);
        var stateCalls = 0;
        var textCalls = 0;
        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 4,
            TotalBytesDownloaded = 500,
            TotalBytesExpected = 500,
            CurrentTrackProgress = 0,
        };

        sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => stateCalls++, () => textCalls++);
        sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => stateCalls++, () => textCalls++);

        Assert.Equal(2, stateCalls);
        Assert.Equal(2, textCalls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_start_preparing_not_throttled_through_handler()
    {
        var manager = new PositionManager();
        var sut = new MessageHandler(manager);
        var stateCalls = 0;
        var textCalls = 0;
        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = 3,
            TotalBytesDownloaded = 0,
            CurrentTrackProgress = 0,
        };

        sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => stateCalls++, () => textCalls++);
        sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => stateCalls++, () => textCalls++);

        Assert.Equal(2, stateCalls);
        Assert.Equal(2, textCalls);
    }

    [Fact]
    public void HandlePreparationProgressMessage_when_throttled_skips_progress_text()
    {
        var manager = new PositionManager();
        var sut = new MessageHandler(manager);
        var stateCalls = 0;
        var textCalls = 0;

        var msg = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 4,
            TotalBytesDownloaded = 10,
            TotalBytesExpected = null,
            CurrentTrackProgress = 0,
        };

        sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => stateCalls++, () => textCalls++);
        sut.HandlePreparationProgressMessage(msg, (_, _, _, _) => stateCalls++, () => textCalls++);

        Assert.Equal(1, stateCalls);
        Assert.Equal(1, textCalls);
    }

    [Fact]
    public void HandlePlaybackPositionMessage_null_position_still_refreshes_progress_text()
    {
        var progressTextCalls = 0;

        MessageHandler.HandlePlaybackPositionMessage(
            new PlaybackPositionChangedMessage(),
            TimeSpan.FromMinutes(1),
            currentUiProgress: 0,
            _ => false,
            _ => { },
            _ => { },
            () => progressTextCalls++);

        Assert.Equal(1, progressTextCalls);
    }

    [Fact]
    public void HandlePlaybackPositionMessage_zero_duration_skips_ignore_predicate_and_formats_time()
    {
        string? timeText = null;
        var progressTextCalls = 0;

        MessageHandler.HandlePlaybackPositionMessage(
            new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(7) },
            TimeSpan.Zero,
            currentUiProgress: 0,
            _ => true,
            t => timeText = t,
            _ => { },
            () => progressTextCalls++);

        Assert.Equal("00:07", timeText);
        Assert.Equal(1, progressTextCalls);
    }
}
