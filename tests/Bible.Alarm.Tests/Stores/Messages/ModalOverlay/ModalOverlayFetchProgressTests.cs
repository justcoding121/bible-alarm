#nullable enable

using Bible.Alarm.Stores.Messages.ModalOverlay;

namespace Bible.Alarm.Tests;

public sealed class ModalOverlayFetchProgressTests
{
    [Fact]
    public void Init_sets_modal_overlay_progress()
    {
        var sut = new ModalOverlayFetchProgress
        {
            ModalType = "BibleSection",
            Progress = 0.75,
            ProgressText = "75%",
            IsVisible = true,
        };

        Assert.Equal("BibleSection", sut.ModalType);
        Assert.Equal(0.75, sut.Progress);
        Assert.Equal("75%", sut.ProgressText);
        Assert.True(sut.IsVisible);
    }
}
