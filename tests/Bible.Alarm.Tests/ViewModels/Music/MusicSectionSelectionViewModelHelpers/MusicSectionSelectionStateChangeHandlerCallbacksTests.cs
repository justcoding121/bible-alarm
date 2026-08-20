#nullable enable

using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionStateChangeHandlerCallbacksTests
{
    [Fact]
    public void Record_invokes_callback_slots_with_expected_values()
    {
        string? lastPub = null;
        string? lastSection = null;
        var initComplete = false;
        var busy = false;
        var initArg = "";
        var selectedCalled = 0;

        var sut = new MusicSectionSelectionStateChangeHandler.Callbacks(
            SetLastPublicationCode: c => lastPub = c,
            SetLastSectionCode: c => lastSection = c,
            GetInitComplete: () => initComplete,
            SetIsBusy: b => busy = b,
            Initialize: s => initArg = s,
            SetSelectedSection: () => selectedCalled++);

        sut.SetLastPublicationCode("pub");
        sut.SetLastSectionCode("sec");
        Assert.Equal("pub", lastPub);
        Assert.Equal("sec", lastSection);

        Assert.False(sut.GetInitComplete());
        initComplete = true;
        Assert.True(sut.GetInitComplete());

        sut.SetIsBusy(true);
        Assert.True(busy);

        sut.Initialize("x");
        Assert.Equal("x", initArg);

        sut.SetSelectedSection();
        sut.SetSelectedSection();
        Assert.Equal(2, selectedCalled);
    }
}
