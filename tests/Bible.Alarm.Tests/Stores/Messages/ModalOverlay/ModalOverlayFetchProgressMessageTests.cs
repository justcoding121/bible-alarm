#nullable enable

using Bible.Alarm.Stores.Messages.ModalOverlay;

namespace Bible.Alarm.Tests;

public sealed class ModalOverlayFetchProgressMessageTests
{
    [Fact]
    public void Value_exposes_payload_from_constructor()
    {
        var payload = new ModalOverlayFetchProgress
        {
            ModalType = "BiblePublication",
            Progress = 0.75,
            ProgressText = "Loading",
            IsVisible = true,
        };

        var sut = new ModalOverlayFetchProgressMessage(payload);

        Assert.Same(payload, sut.Value);
    }
}
