#nullable enable

using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionRefreshContextTests
{
    [Fact]
    public async Task Init_wires_populate_sections_and_other_setters()
    {
        var populateCalls = 0;
        var busy = false;
        var selected = 0;

        var sut = new MusicSectionSelectionRefreshContext
        {
            IsDisposed = () => false,
            IsSelectingSection = () => false,
            SetIsBusy = b => busy = b,
            SetCanCancelFetch = _ => { },
            SetShowProgress = _ => { },
            SetProgressText = _ => { },
            SetProgressPercent = _ => { },
            SetScreenOn = _ => { },
            PopulateSections = async (_, _) =>
            {
                populateCalls++;
                await Task.CompletedTask;
            },
            SetSelectedSection = () => selected++
        };

        sut.SetIsBusy(true);
        Assert.True(busy);

        await sut.PopulateSections!("pub", null);
        Assert.Equal(1, populateCalls);

        sut.SetSelectedSection();
        Assert.Equal(1, selected);
    }
}
