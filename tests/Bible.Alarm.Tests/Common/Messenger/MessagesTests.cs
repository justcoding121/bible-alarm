#nullable enable

using Bible.Alarm.Common.Messenger;

namespace Bible.Alarm.Tests;

public sealed class MessengerMessagesTests
{
    [Fact]
    public void Messenger_message_types_smoke_construct()
    {
        _ = new InitializedMessage();

        var toast = new ShowToastMessage("hello");
        Assert.Equal("hello", toast.Value);

        var pos = new PlaybackPositionChangedMessage
        {
            CurrentPosition = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromMinutes(2)
        };
        Assert.Equal(TimeSpan.FromSeconds(1), pos.CurrentPosition);
        Assert.Equal(TimeSpan.FromMinutes(2), pos.Duration);

        var prep = new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 1,
            TotalTracks = 10,
            CurrentTrackProgress = 0.5,
            BytesDownloaded = 100,
            TotalBytes = 200,
            TotalBytesDownloaded = 50,
            TotalBytesExpected = 500,
            ShowPercent = true
        };
        Assert.Equal(1, prep.LoadedTracks);
        Assert.Equal(10, prep.TotalTracks);
        Assert.Equal(0.5, prep.CurrentTrackProgress);
        Assert.Equal(100L, prep.BytesDownloaded);
        Assert.Equal(200L, prep.TotalBytes);
        Assert.Equal(50L, prep.TotalBytesDownloaded);
        Assert.Equal(500L, prep.TotalBytesExpected);
        Assert.True(prep.ShowPercent);

        _ = new NextButtonPressedMessage();
        _ = new PreviousButtonPressedMessage();
        _ = new PlayButtonPressedMessage();
        _ = new PauseButtonPressedMessage();
        _ = new TogglePlayPauseMessage();
        _ = new SeekForwardButtonPressedMessage();
        _ = new SeekBackwardButtonPressedMessage();
        _ = new DestroyMediaElementMessage();
        _ = new ShowProgressBarMessage();
        _ = new HideProgressBarMessage();
        _ = new ThemeChangedMessage();
        _ = new BeginStoppingPlaybackMessage();
        _ = new MinimizePlaybackMessage();
        _ = new MaximizePlaybackMessage();
        _ = new PlaybackExplicitStopMessage();

        var showModal = new RequestShowPlaybackModalMessage { TargetScheduleId = 42 };
        Assert.Equal(42, showModal.TargetScheduleId);

        _ = new PlaybackModalOpenedMessage();
    }
}
